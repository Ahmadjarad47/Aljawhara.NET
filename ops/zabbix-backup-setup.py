#!/usr/bin/env python3
import json
import pathlib
import secrets
import sys
import urllib.request

API = "http://127.0.0.1:8080/zabbix/api_jsonrpc.php"
CREDENTIALS = pathlib.Path("/root/.zabbix_admin_credentials")
HOST = "aljawhara-prod"


def read_credentials():
    values = {}
    for line in CREDENTIALS.read_text(encoding="utf-8").splitlines():
        if ":" in line:
            key, value = line.split(":", 1)
            values[key.strip().lower()] = value.strip()
    return values["username"], values["password"]


class Zabbix:
    def __init__(self, username, password):
        self.request_id = 0
        self.auth = None
        self.auth = self.call("user.login", {"username": username, "password": password}, authenticate=False)

    def call(self, method, params, authenticate=True):
        self.request_id += 1
        payload = {"jsonrpc": "2.0", "method": method, "params": params, "id": self.request_id}
        if authenticate:
            payload["auth"] = self.auth
        request = urllib.request.Request(
            API,
            data=json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json-rpc"},
        )
        with urllib.request.urlopen(request, timeout=30) as response:
            result = json.load(response)
        if "error" in result:
            error = result["error"]
            raise RuntimeError(f"{method}: {error.get('message')}: {error.get('data')}")
        return result["result"]


def ensure_host(api):
    hosts = api.call("host.get", {"output": ["hostid"], "filter": {"host": [HOST]}})
    if hosts:
        return hosts[0]["hostid"], False

    groups = api.call("hostgroup.get", {"output": ["groupid"], "filter": {"name": ["Linux servers"]}})
    if groups:
        group_id = groups[0]["groupid"]
    else:
        group_id = api.call("hostgroup.create", {"name": "Linux servers"})["groupids"][0]
    result = api.call(
        "host.create",
        {
            "host": HOST,
            "name": "Aljawhara.NET Production",
            "groups": [{"groupid": group_id}],
            "interfaces": [
                {"type": 1, "main": 1, "useip": 1, "ip": "127.0.0.1", "dns": "", "port": "10050"}
            ],
            "tags": [{"tag": "application", "value": "aljawhara"}, {"tag": "scope", "value": "backup"}],
        },
    )
    return result["hostids"][0], True


ITEMS = [
    ("Backup: Last successful verified backup", "last_success_epoch", "unixtime"),
    ("Backup: Job failure flag", "backup_job_failure", ""),
    ("Backup: Missing scheduled backup flag", "missing_scheduled", ""),
    ("Backup: Restore verification failure flag", "verification_failure", ""),
    ("Backup: Verified retained sets", "verified_count", ""),
    ("Backup: Completed dump files", "completed_count", ""),
    ("Backup: Directory size", "directory_bytes", "B"),
    ("Backup: Filesystem free space", "disk_free_bytes", "B"),
    ("Backup: Last successful restore test", "last_restore_epoch", "unixtime"),
    ("Backup: Incomplete partial files", "partial_count", ""),
    ("Backup: Last application completion log", "last_backup_log_epoch", "unixtime"),
]


def ensure_items(api, host_id):
    created = 0
    updated = 0
    interfaces = api.call(
        "hostinterface.get", {"output": ["interfaceid"], "hostids": [host_id], "filter": {"type": 1, "main": 1}}
    )
    if not interfaces:
        raise RuntimeError("The Aljawhara Zabbix host has no main agent interface")
    interface_id = interfaces[0]["interfaceid"]
    existing = api.call("item.get", {"output": ["itemid", "key_"], "hostids": [host_id]})
    by_key = {item["key_"]: item["itemid"] for item in existing}
    for name, metric, units in ITEMS:
        key = f"aljawhara.backup.metric[{metric}]"
        settings = {
            "name": name,
            "key_": key,
            "hostid": host_id,
            "interfaceid": interface_id,
            "type": 0,
            "value_type": 3,
            "delay": "60s",
            "history": "30d",
            "trends": "365d",
            "units": units,
            "tags": [{"tag": "component", "value": "backup"}],
        }
        if key in by_key:
            settings["itemid"] = by_key[key]
            settings.pop("hostid")
            settings.pop("key_")
            api.call("item.update", settings)
            updated += 1
        else:
            api.call("item.create", settings)
            created += 1
    return created, updated


TRIGGERS = [
    ("Aljawhara backup job failed", "last(/aljawhara-prod/aljawhara.backup.metric[backup_job_failure])=1", 4),
    ("Aljawhara scheduled backup is missing", "last(/aljawhara-prod/aljawhara.backup.metric[missing_scheduled])=1", 4),
    ("Aljawhara backup restore verification failed", "last(/aljawhara-prod/aljawhara.backup.metric[verification_failure])=1", 4),
    ("Aljawhara has fewer than 3 verified backup sets", "last(/aljawhara-prod/aljawhara.backup.metric[verified_count])<3", 2),
    ("Aljawhara backup filesystem has less than 5 GiB free", "last(/aljawhara-prod/aljawhara.backup.metric[disk_free_bytes])<5368709120", 4),
    ("Aljawhara restore test is older than 36 hours", "now()-last(/aljawhara-prod/aljawhara.backup.metric[last_restore_epoch])>129600", 4),
    ("Aljawhara backup partial file persists", "min(/aljawhara-prod/aljawhara.backup.metric[partial_count],2h)>0", 3),
]


def ensure_triggers(api, host_id):
    created = 0
    updated = 0
    existing = api.call("trigger.get", {"output": ["triggerid", "description"], "hostids": [host_id]})
    by_name = {trigger["description"]: trigger["triggerid"] for trigger in existing}
    for description, expression, priority in TRIGGERS:
        settings = {
            "description": description,
            "expression": expression,
            "priority": priority,
            "manual_close": 1,
            "tags": [{"tag": "component", "value": "backup"}, {"tag": "service", "value": "aljawhara"}],
        }
        if description in by_name:
            settings["triggerid"] = by_name[description]
            api.call("trigger.update", settings)
            updated += 1
        else:
            api.call("trigger.create", settings)
            created += 1
    return created, updated


def rotate_admin_password(api, username, current_password):
    users = api.call("user.get", {"output": ["userid"], "filter": {"username": [username]}})
    if not users:
        raise RuntimeError("Could not find the Zabbix administrator for password rotation")
    new_password = secrets.token_urlsafe(32)
    api.call(
        "user.update",
        {"userid": users[0]["userid"], "passwd": new_password, "current_passwd": current_password},
    )
    temp = CREDENTIALS.with_suffix(".tmp")
    temp.write_text(f"Username: {username}\nPassword: {new_password}\n", encoding="utf-8")
    temp.chmod(0o600)
    temp.replace(CREDENTIALS)


def main():
    username, password = read_credentials()
    api = Zabbix(username, password)
    host_id, host_created = ensure_host(api)
    item_created, item_updated = ensure_items(api, host_id)
    trigger_created, trigger_updated = ensure_triggers(api, host_id)
    if "--rotate-admin" in sys.argv:
        rotate_admin_password(api, username, password)
    print(
        "ZABBIX_BACKUP_SETUP=OK "
        f"host_created={int(host_created)} items_created={item_created} items_updated={item_updated} "
        f"triggers_created={trigger_created} triggers_updated={trigger_updated} "
        f"admin_password_rotated={int('--rotate-admin' in sys.argv)}"
    )


if __name__ == "__main__":
    main()
