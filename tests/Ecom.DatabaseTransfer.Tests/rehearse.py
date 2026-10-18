#!/usr/bin/env python3
"""Real restored-source/PG18 tests. Run BEFORE importing into the empty test target.
Uses SELECT only on SQL Server. Target writes occur only through the transfer tool.
No database is dropped, recreated, truncated or cleared by this test runner.
"""
import os, subprocess, sys
binary = os.path.abspath(sys.argv[1])

def run(flags, expected):
    result = subprocess.run([binary, *flags], capture_output=True, text=True)
    print(result.stdout, end=''); print(result.stderr, end='')
    assert result.returncode == expected, (flags, result.returncode)
    return result.stdout + result.stderr

assert 'Database=dbOfEcom_migration_source' in os.environ['SQLSERVER_SOURCE_CONNECTION']
assert 'Database=ecom_migration_test' in os.environ['ConnectionStrings__DefaultConnection']
def snapshot():
    # Read every table count plus sequence state, without consuming nextval().
    base=['psql','-X','-At','-h','/var/run/postgresql','-U','postgres','-d','ecom_migration_test']
    queries=subprocess.check_output(base+['-c',"SELECT format('SELECT %L, count(*) FROM public.%I;',tablename,tablename) FROM pg_tables WHERE schemaname='public' ORDER BY tablename"],text=True)
    counts=subprocess.check_output(base,input=queries,text=True)
    seqs=subprocess.check_output(base+['-c',"SELECT format('SELECT %L,last_value,is_called FROM public.%I;',sequencename,sequencename) FROM pg_sequences WHERE schemaname='public' ORDER BY sequencename"],text=True)
    return counts,subprocess.check_output(base,input=seqs,text=True)

baseline=snapshot()
run(['--preflight'], 0)
assert snapshot()==baseline, 'Preflight changed target state'
# A full successful import followed by deliberate rollback exercises COPY, all row
# fingerprints, financial totals, FKs, timestamp archive, counters and sequences.
run(['--source-times-are-utc', '--rollback-test'], 0)
assert snapshot()==baseline, 'Rollback changed rows/schema/sequence state'
# Repetition succeeds only if no application records or timestamp archive survived.
run(['--source-times-are-utc', '--simulate-failure-before-commit'], 1)
assert snapshot()==baseline, 'Injected failure left target changes'
run(['--source-times-are-utc', '--rollback-test'], 0)
assert snapshot()==baseline, 'Rollback changed rows/schema/sequence state'
print('PASS: real SQL Server -> PostgreSQL transactional rehearsals and failure recovery')

