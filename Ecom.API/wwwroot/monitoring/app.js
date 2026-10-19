'use strict';
(() => {
  const $ = id => document.getElementById(id);
  // The token stays in memory. Reloading or signing out clears the session.
  let token = '', page = 1, busy = false, total = 0, currentFilters = {};
  let generation = 0;
  const number = value => new Intl.NumberFormat().format(value);
  const time = value => value ? new Date(value).toLocaleString() : 'Never';
  const text = (tag, value, className) => {
    const element = document.createElement(tag);
    element.textContent = value;
    if (className) element.className = className;
    return element;
  };
  function signOut(message = '') {
    generation++;
    token = '';
    $('dashboard').hidden = true;
    $('session').hidden = true;
    $('login-panel').hidden = false;
    $('login-error').textContent = message;
    $('logs').replaceChildren();
    $('password').value = '';
  }
  async function api(path) {
    const response = await fetch('/api/admin/monitoring/' + path, {
      headers: { Authorization: 'Bearer ' + token }, cache: 'no-store', signal: AbortSignal.timeout(12000)
    });
    if (response.status === 401 || response.status === 403) {
      signOut('Administrator access is required. Please sign in again.');
      throw new Error('Session ended.');
    }
    if (!response.ok) throw new Error('Unable to load monitoring data. Please try again.');
    return response.json();
  }
  function renderSummary(data) {
    const checkedAt = data.health.checkedAt;
    const stale = !checkedAt || Date.now() - new Date(checkedAt).getTime() > 360000;
    $('health').textContent = stale ? 'Unknown' : data.health.status;
    $('health').className = stale ? 'warn' : data.health.status === 'Healthy' ? 'good' : 'bad';
    $('health-time').textContent = checkedAt ? 'Checked ' + time(checkedAt) : 'Waiting for first check';
    $('requests').textContent = number(data.requests);
    $('uptime').textContent = 'Uptime ' + Math.floor(data.uptimeSeconds / 3600) + 'h ' + Math.floor(data.uptimeSeconds % 3600 / 60) + 'm';
    $('errors').textContent = number(data.serverErrors);
    $('errors').className = data.serverErrors ? 'bad' : '';
    $('error-rate').textContent = data.errorRatePercent + '% error rate · ' + number(data.clientErrors) + ' client errors';
    $('latency').textContent = data.averageMs + ' ms';
    $('p95').textContent = data.requests === 0 ? 'No traffic in this window' : data.p95UpperBoundMs == null ? '95th percentile above 30s' : '95th percentile ≤ ' + data.p95UpperBoundMs + ' ms';
    $('memory').textContent = data.workingSetMb + ' MB memory';
    $('retention').textContent = 'Latest ' + number(data.logging.recentEventLimit) + ' events · disk retention up to ' + data.logging.retentionDays + ' days';
    const notices = [];
    if (data.logging.persistenceError) notices.push(data.logging.persistenceError);
    if (data.logging.droppedEvents) notices.push(number(data.logging.droppedEvents) + ' events could not be saved to disk.');
    $('notice').textContent = notices.join(' ');
    const max = Math.max(1, ...data.series.map(point => point.requests));
    $('chart').replaceChildren(...data.series.map(point => {
      const column = text('div', '', 'chart-column');
      column.title = time(point.timestamp) + ': ' + point.requests + ' requests, ' + point.errors + ' errors';
      const bar = text('div', '', 'bar');
      bar.style.height = (point.requests / max * 100) + '%';
      column.append(bar);
      if (point.errors) {
        const error = text('div', '', 'error-bar');
        error.style.height = (point.errors / max * 100) + '%';
        column.append(error);
      }
      return column;
    }));
    $('chart').setAttribute('aria-label', data.requests + ' requests and ' + data.serverErrors + ' server errors in the last 60 minutes.');
    $('jobs').replaceChildren(...data.jobs.map(job => {
      const card = text('div', '', 'job');
      card.append(text('span', job.status, 'badge ' + (job.status === 'Healthy' ? 'good' : job.status === 'Starting' ? 'warn' : 'bad')),
        text('strong', job.name), text('p', 'Last success: ' + time(job.lastSuccess)));
      return card;
    }));
    if (!data.jobs.length) $('jobs').append(text('p', 'No background jobs registered in this environment.', 'muted'));
    $('updated').textContent = 'Updated ' + new Date().toLocaleTimeString();
  }
  function renderLogs(data) {
    total = data.totalCount;
    const rows = data.items.map(entry => {
      const row = document.createElement('tr');
      row.append(text('td', time(entry.timestamp)));
      const levelCell = document.createElement('td');
      levelCell.append(text('span', entry.level, 'badge ' + (['Error','Critical'].includes(entry.level) ? 'bad' : entry.level === 'Warning' ? 'warn' : '')));
      row.append(levelCell);
      const message = text('td', entry.message);
      message.append(text('small', entry.category));
      if (entry.exceptionType) message.append(text('small', entry.exceptionType, 'bad'));
      const details = document.createElement('details');
      details.append(text('summary', 'Event details'), text('pre', JSON.stringify({userId: entry.userId, ...entry.properties}, null, 2)));
      message.append(details);
      row.append(message);
      const trace = document.createElement('td');
      if (entry.traceId) {
        const button = text('button', entry.traceId, 'trace-button');
        button.title = 'Show events from this request';
        button.addEventListener('click', () => { $('trace').value = entry.traceId; applyFilters(); });
        trace.append(button);
      } else trace.textContent = '—';
      row.append(trace);
      return row;
    });
    if (!rows.length) {
      const row = document.createElement('tr'), cell = text('td', 'No events match these filters.', 'muted');
      cell.colSpan = 4; row.append(cell); rows.push(row);
    }
    $('logs').replaceChildren(...rows);
    $('log-count').textContent = number(total) + ' matching events · ' + number(data.retainedCount) + ' recent events available';
    $('page-number').textContent = 'Page ' + page;
    $('previous').disabled = page <= 1;
    $('next').disabled = page * 50 >= total;
  }
  async function refresh() {
    if (!token || busy) return;
    busy = true;
    const version = generation;
    $('refresh').disabled = true;
    try {
      const query = new URLSearchParams({...currentFilters, page: String(page), pageSize: '50'});
      const [summary, logs] = await Promise.all([api('summary'), api('logs?' + query)]);
      if (token && version === generation) { renderSummary(summary); renderLogs(logs); }
    } catch (error) {
      if (token && version === generation) $('notice').textContent = error.message + ' Displayed data may be out of date.';
    } finally { busy = false; $('refresh').disabled = false; }
  }
  function applyFilters() {
    if (busy) return;
    currentFilters = {};
    for (const [key, id] of [['search', 'search'], ['level', 'level'], ['traceId', 'trace']]) {
      const value = $(id).value.trim(); if (value) currentFilters[key] = value;
    }
    page = 1; refresh();
  }
  $('login-form').addEventListener('submit', async event => {
    event.preventDefault();
    $('login-button').disabled = true;
    $('login-error').textContent = '';
    try {
      const response = await fetch('/api/auth/login', {method: 'POST', headers: {'Content-Type': 'application/json'},
        body: JSON.stringify({email: $('email').value, password: $('password').value}), signal: AbortSignal.timeout(15000)});
      $('password').value = '';
      if (!response.ok) throw new Error(response.status === 429 ? 'Too many attempts. Please try again later.' : 'Sign-in failed. Check your account details.');
      const result = await response.json();
      token = result.data?.token || '';
      if (!token) throw new Error('Sign-in did not return a session.');
      await api('summary');
      $('login-panel').hidden = true; $('dashboard').hidden = false; $('session').hidden = false;
      page = 1; await refresh();
    } catch (error) { signOut(error.message); }
    finally { $('login-button').disabled = false; }
  });
  $('logout').addEventListener('click', () => signOut());
  $('refresh').addEventListener('click', refresh);
  $('filters').addEventListener('submit', event => {event.preventDefault(); applyFilters();});
  $('clear').addEventListener('click', () => { $('filters').reset(); applyFilters(); });
  $('previous').addEventListener('click', () => {if (!busy && page > 1) { page--; refresh(); }});
  $('next').addEventListener('click', () => {if (!busy && page * 50 < total) { page++; refresh(); }});
  document.addEventListener('visibilitychange', () => {if (!document.hidden) refresh();});
  setInterval(() => {if (!document.hidden) refresh();}, 15000);
})();
