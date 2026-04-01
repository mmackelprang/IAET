#!/usr/bin/env python3
"""Generate an IAET investigation dashboard HTML.

Usage:
  python3 scripts/generate-dashboard.py                       # all projects
  python3 scripts/generate-dashboard.py .iaet-projects/myproj  # single project
"""

import json
import os
import sys
import html
import re
from datetime import datetime, timezone


def read(path):
    with open(path, 'r', encoding='utf-8') as f:
        return f.read()


def read_optional(path):
    try:
        return read(path)
    except FileNotFoundError:
        return None


def load_cookies(project_dir, knowledge_dir):
    """Load the most recent cookie snapshot for a project.

    Looks in .iaet-projects/{name}/cookies/ for FileCookieStore snapshots first,
    then falls back to knowledge/cookies.json. Cookie values are NEVER included
    in the returned data — only names and metadata.
    """
    cookies_dir = os.path.join(project_dir, 'cookies')
    result = {'cookies': [], 'snapshotTime': None, 'source': None}

    # Try FileCookieStore snapshot directory first
    if os.path.exists(cookies_dir):
        snapshots = []
        for f in os.listdir(cookies_dir):
            if not f.endswith('.json'):
                continue
            try:
                data = json.loads(read(os.path.join(cookies_dir, f)))
                captured_at = data.get('capturedAt', '')
                snapshots.append((captured_at, data))
            except (json.JSONDecodeError, IOError):
                continue

        if snapshots:
            # Sort by capturedAt descending, pick the most recent
            snapshots.sort(key=lambda x: x[0], reverse=True)
            _, latest = snapshots[0]
            result['snapshotTime'] = latest.get('capturedAt')
            result['source'] = latest.get('source', 'unknown')
            result['cookies'] = _strip_cookie_values(latest.get('cookies', []))
            return result

    # Fallback: knowledge/cookies.json
    cookies_knowledge = read_optional(os.path.join(knowledge_dir, 'cookies.json'))
    if cookies_knowledge:
        try:
            data = json.loads(cookies_knowledge)
            raw_cookies = data if isinstance(data, list) else data.get('cookies', [])
            result['cookies'] = _strip_cookie_values(raw_cookies)
            result['source'] = 'knowledge/cookies.json'
        except json.JSONDecodeError:
            pass

    return result


def _strip_cookie_values(cookies):
    """Return cookie metadata only — NEVER include cookie values."""
    stripped = []
    for c in cookies:
        stripped.append({
            'name': c.get('name', ''),
            'domain': c.get('domain', ''),
            'path': c.get('path', '/'),
            'httpOnly': c.get('httpOnly', False),
            'secure': c.get('secure', False),
            'sameSite': c.get('sameSite', ''),
            'expires': c.get('expires'),
            'size': c.get('size', 0),
        })
    return stripped


def load_project(project_dir):
    """Load all data for a single project."""
    output_dir = os.path.join(project_dir, 'output')
    knowledge_dir = os.path.join(project_dir, 'knowledge')
    captures_dir = os.path.join(project_dir, 'captures')

    config_text = read_optional(os.path.join(project_dir, 'project.json'))
    config = json.loads(config_text) if config_text else {}

    project = {
        'name': config.get('name', os.path.basename(project_dir)),
        'displayName': config.get('displayName', os.path.basename(project_dir)),
        'status': config.get('status', 'unknown'),
        'targetType': config.get('targetType', 'web'),
        'currentRound': config.get('currentRound', 0),
    }

    # Entry points
    entry_points = config.get('entryPoints', [])
    project['url'] = entry_points[0]['url'] if entry_points else 'unknown'

    # Load outputs
    project['narrative'] = read_optional(os.path.join(output_dir, 'narrative.md')) or ''
    project['openapi'] = read_optional(os.path.join(output_dir, 'api.yaml')) or ''
    project['clientPrompt'] = read_optional(os.path.join(output_dir, 'client-prompt.md')) or ''

    # Load diagrams
    project['diagrams'] = {}
    ddir = os.path.join(output_dir, 'diagrams')
    if os.path.exists(ddir):
        for f in sorted(os.listdir(ddir)):
            if f.endswith('.mmd'):
                project['diagrams'][f] = read(os.path.join(ddir, f))

    # Load knowledge
    project['knowledge'] = {}
    if os.path.exists(knowledge_dir):
        for f in sorted(os.listdir(knowledge_dir)):
            if f.endswith('.json'):
                project['knowledge'][f] = read(os.path.join(knowledge_dir, f))

    # Load capture archive info
    project['captures'] = []
    if os.path.exists(captures_dir):
        for f in sorted(os.listdir(captures_dir)):
            if f.endswith('.gz') or f.endswith('.json'):
                size = os.path.getsize(os.path.join(captures_dir, f))
                project['captures'].append({'name': f, 'size': size})

    # Load cookie snapshots (for web projects)
    project['cookies'] = load_cookies(project_dir, knowledge_dir)

    # Parse knowledge for stats
    endpoints_data = json.loads(project['knowledge'].get('endpoints.json', '{}'))
    protocols_data = json.loads(project['knowledge'].get('protocols.json', '{}'))
    deps_data = json.loads(project['knowledge'].get('dependencies.json', '{}'))

    project['stats'] = {
        'endpoints': len(endpoints_data.get('endpoints', [])),
        'streams': len(protocols_data.get('streams', [])),
        'diagrams': len(project['diagrams']),
        'authChains': len(deps_data.get('authChains', [])),
        'captures': len(project['captures']),
        'cookies': len(project['cookies'].get('cookies', [])),
        'prompt': 1 if project['clientPrompt'] else 0,
    }

    # Build next steps
    project['nextSteps'] = build_next_steps(
        endpoints_data, protocols_data, deps_data,
        project['narrative'], project['openapi']
    )

    return project


def build_next_steps(endpoints_data, protocols_data, deps_data, narrative, openapi):
    """Analyze project data and generate actionable next steps."""
    steps = []
    all_text = openapi + narrative + json.dumps(protocols_data) + json.dumps(deps_data)

    # Find IP addresses
    ip_addresses = set(re.findall(r'\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b', all_text))
    ip_addresses = [ip for ip in ip_addresses if not ip.startswith('0.') and not ip.startswith('127.')]

    for ip in sorted(ip_addresses):
        steps.append({
            'category': 'Resolve IP Addresses', 'type': 'human',
            'item': f'Resolve <code>{ip}</code> to hostname',
            'detail': f'Run <code>nslookup {ip}</code> or <code>dig -x {ip}</code>. Consider adding automated reverse DNS to IAET.',
        })

    if 'non-JSON' in narrative or 'protobuf' in narrative.lower():
        steps.append({
            'category': 'Protocol Decoding', 'type': 'tooling',
            'item': 'Add protobuf/protojson decoder to schema inferrer',
            'detail': 'Positional JSON arrays need a protojson-aware inferrer to map array positions to field names.',
        })
        steps.append({
            'category': 'Protocol Decoding', 'type': 'human',
            'item': 'Try <code>?alt=json</code> query parameter on API requests',
            'detail': 'Some Google APIs support standard JSON output which the existing schema inferrer can parse.',
        })

    # API completeness predictions
    sigs = [e['signature'] for e in endpoints_data.get('endpoints', [])]
    if any('list' in s for s in sigs) and not any('/get' in s.split('/')[-1] for s in sigs):
        steps.append({
            'category': 'API Completeness', 'type': 'human',
            'item': 'Predicted: single-item GET endpoints for discovered list endpoints',
            'detail': 'List endpoints exist but no corresponding single-item GET. Navigate to specific items to discover.',
        })

    steps.append({
        'category': 'Tooling Improvements', 'type': 'tooling',
        'item': 'Automatic reverse DNS for IP addresses in captures',
        'detail': 'Resolve IPs found in SDP, ICE candidates, and API responses to hostnames automatically.',
    })
    steps.append({
        'category': 'Output Generation', 'type': 'tooling',
        'item': 'Generate API client prompt for AI code generation',
        'detail': 'Package OpenAPI spec, auth chains, and protocol details into a prompt for AI-assisted client generation.',
    })
    steps.append({
        'category': 'Output Generation', 'type': 'tooling',
        'item': 'API Expert Agent — predict undiscovered endpoints',
        'detail': 'Add an agent that reviews output as an expert API designer, identifying gaps and feeding them back.',
    })

    return steps


def render_project_content(project):
    """Render all tabs for a single project as HTML."""
    p = project

    # Diagrams
    diagram_html = ""
    for name, content in p['diagrams'].items():
        title = name.replace('.mmd', '').replace('-', ' ').title()
        # Use <pre class="mermaid"> with html.escape().  Mermaid.js reads
        # textContent of <pre> elements, which auto-unescapes HTML entities
        # back to the original characters.  This is the officially recommended
        # approach — it prevents the browser from interpreting <br/> or other
        # HTML-like syntax in Mermaid source as real HTML tags.
        diagram_html += f'''<div class="diagram-card"><h3>{html.escape(title)}</h3>
      <pre class="mermaid">{html.escape(content)}</pre>
      <details><summary>View Source (.mmd)</summary><pre><code>{html.escape(content)}</code></pre></details></div>\n'''

    # Knowledge
    knowledge_html = ""
    for name, content in p['knowledge'].items():
        title = name.replace('.json', '').replace('-', ' ').title()
        knowledge_html += f'''<div class="data-card"><h3>{html.escape(title)}</h3>
      <pre><code class="language-json">{html.escape(content)}</code></pre></div>\n'''

    # Captures
    captures_html = ""
    if p['captures']:
        captures_html = '<div class="data-card"><h3>Archived Captures</h3><table style="width:100%;border-collapse:collapse;font-size:13px;">'
        captures_html += '<tr><th style="text-align:left;padding:6px;border-bottom:1px solid var(--border);">File</th><th style="text-align:right;padding:6px;border-bottom:1px solid var(--border);">Size</th></tr>'
        for c in p['captures']:
            size_str = f"{c['size'] / 1024:.0f} KB" if c['size'] < 1024 * 1024 else f"{c['size'] / (1024*1024):.1f} MB"
            captures_html += f'<tr><td style="padding:6px;border-bottom:1px solid #1e293b;">{html.escape(c["name"])}</td><td style="text-align:right;padding:6px;border-bottom:1px solid #1e293b;">{size_str}</td></tr>'
        captures_html += '</table></div>'

    # Next steps
    next_steps_html = ""
    current_cat = ""
    for step in p['nextSteps']:
        if step['category'] != current_cat:
            if current_cat:
                next_steps_html += "</div>\n"
            current_cat = step['category']
            next_steps_html += f'<h3>{html.escape(current_cat)}</h3>\n<div class="steps-group">\n'
        badge_class = 'badge-tooling' if step['type'] == 'tooling' else 'badge-human'
        badge_text = 'Tooling' if step['type'] == 'tooling' else 'Human'
        next_steps_html += f'''<div class="step-card">
      <div class="step-header">{step['item']} <span class="{badge_class}">{badge_text}</span></div>
      <div class="step-detail">{step['detail']}</div></div>\n'''
    if current_cat:
        next_steps_html += "</div>\n"

    # Cookies (web projects only)
    cookies_html = ""
    cookie_data = p.get('cookies', {})
    cookie_list = cookie_data.get('cookies', [])
    is_web = p.get('targetType', 'web') == 'web'

    if is_web and cookie_list:
        snapshot_time = cookie_data.get('snapshotTime', 'unknown')
        source = cookie_data.get('source', '')
        cookies_html += f'<div class="data-card"><h3>Cookie Snapshot</h3>'
        cookies_html += f'<p style="color:var(--muted);font-size:13px;margin-bottom:12px;">'
        cookies_html += f'{len(cookie_list)} cookies captured'
        if snapshot_time:
            cookies_html += f' &mdash; {html.escape(str(snapshot_time))}'
        if source:
            cookies_html += f' (source: {html.escape(source)})'
        cookies_html += '</p>'
        cookies_html += '<table style="width:100%;border-collapse:collapse;font-size:13px;">'
        cookies_html += '<tr>'
        for hdr in ['Name', 'Domain', 'Path', 'HttpOnly', 'Secure', 'SameSite', 'Expires']:
            cookies_html += f'<th style="text-align:left;padding:6px;border-bottom:1px solid var(--border);color:var(--accent);font-size:12px;">{hdr}</th>'
        cookies_html += '</tr>'
        for c in cookie_list:
            httponly_icon = 'Yes' if c.get('httpOnly') else 'No'
            secure_icon = 'Yes' if c.get('secure') else 'No'
            same_site = html.escape(str(c.get('sameSite', '') or ''))
            expires = html.escape(str(c.get('expires', '') or 'Session'))
            cookies_html += '<tr>'
            cookies_html += f'<td style="padding:6px;border-bottom:1px solid #1e293b;font-family:monospace;font-size:12px;">{html.escape(c.get("name", ""))}</td>'
            cookies_html += f'<td style="padding:6px;border-bottom:1px solid #1e293b;">{html.escape(c.get("domain", ""))}</td>'
            cookies_html += f'<td style="padding:6px;border-bottom:1px solid #1e293b;">{html.escape(c.get("path", "/"))}</td>'
            cookies_html += f'<td style="padding:6px;border-bottom:1px solid #1e293b;">{httponly_icon}</td>'
            cookies_html += f'<td style="padding:6px;border-bottom:1px solid #1e293b;">{secure_icon}</td>'
            cookies_html += f'<td style="padding:6px;border-bottom:1px solid #1e293b;">{same_site}</td>'
            cookies_html += f'<td style="padding:6px;border-bottom:1px solid #1e293b;font-size:12px;">{expires}</td>'
            cookies_html += '</tr>'
        cookies_html += '</table></div>'
    elif is_web:
        cookies_html = '<p class="subtitle">No cookie snapshots captured yet.</p>'

    # Client prompt
    prompt_html = ""
    if p.get('clientPrompt'):
        prompt_html = f'<div class="narrative" id="prompt-content-{html.escape(p["name"])}"></div>'
    else:
        prompt_html = '<p class="subtitle">No client prompt generated yet. Run <code>iaet export smart-client-prompt --project {name}</code> to generate.</p>'.format(name=html.escape(p['name']))

    # Status bar
    status = p.get('status', 'unknown')
    status_class = {
        'new': 'status-new',
        'investigating': 'status-investigating',
        'complete': 'status-complete',
        'archived': 'status-archived',
    }.get(status, 'status-new')
    status_bar = f'''<div class="status-bar">
      <span class="status-badge {status_class}">{html.escape(status.title())}</span>
      <span class="status-actions">Mark as:
        <code>iaet project complete --name {html.escape(p['name'])}</code> |
        <code>iaet project rerun --name {html.escape(p['name'])}</code>
      </span>
    </div>'''

    return {
        'diagrams': diagram_html or '<p class="subtitle">No diagrams generated yet.</p>',
        'narrative': json.dumps(p['narrative']),
        'knowledge': knowledge_html + captures_html or '<p class="subtitle">No knowledge base yet.</p>',
        'openapi': html.escape(p['openapi']) if p['openapi'] else 'No OpenAPI spec generated yet.',
        'cookies': cookies_html,
        'prompt': prompt_html,
        'nextsteps': next_steps_html or '<p class="subtitle">No next steps identified.</p>',
        'statusBar': status_bar,
    }


def main():
    projects_root = '.iaet-projects'

    # Determine which projects to include
    if len(sys.argv) > 1 and os.path.isdir(sys.argv[1]):
        project_dirs = [sys.argv[1]]
    else:
        if not os.path.exists(projects_root):
            print(f"No projects directory found at {projects_root}")
            sys.exit(1)
        project_dirs = [os.path.join(projects_root, d) for d in sorted(os.listdir(projects_root))
                       if os.path.isdir(os.path.join(projects_root, d))]

    if not project_dirs:
        print("No projects found.")
        sys.exit(1)

    projects = []
    for d in project_dirs:
        try:
            projects.append(load_project(d))
        except Exception as e:
            print(f"Warning: Could not load project {d}: {e}")

    # Build project selector HTML
    selector_html = ""
    if len(projects) > 1:
        options = ""
        for i, p in enumerate(projects):
            selected = ' selected' if i == 0 else ''
            options += f'<option value="{i}"{selected}>{html.escape(p["displayName"])} ({p["status"]})</option>\n'
        selector_html = f'''<div style="margin-bottom:16px;display:flex;align-items:center;gap:12px;">
      <label style="color:var(--muted);font-size:14px;">Project:</label>
      <select id="project-selector" onchange="switchProject(this.value)" style="background:var(--card);color:var(--text);border:1px solid var(--border);padding:8px 12px;border-radius:6px;font-size:14px;min-width:200px;">
        {options}
      </select></div>'''

    # Render each project's content
    all_content = []
    for p in projects:
        all_content.append(render_project_content(p))

    # Build per-project content divs
    project_divs = ""
    for i, (p, content) in enumerate(zip(projects, all_content)):
        display = 'block' if i == 0 else 'none'
        s = p['stats']
        has_prompt = s.get('prompt', 0) > 0
        prompt_tab = '<div class="tab" data-tab="prompt" onclick="showTab(\'prompt\')">Prompt</div>' if has_prompt else ''
        prompt_stat = f'<div class="stat" onclick="showTab(\'prompt\')"><div class="stat-value">{s.get("prompt", 0)}</div><div class="stat-label">Prompt</div></div>' if has_prompt else ''
        project_divs += f'''
<div class="project-content" id="project-{i}" style="display:{display}">
  {content['statusBar']}
  <p class="subtitle">{html.escape(p['displayName'])} — {html.escape(p['url'])}</p>
  <div class="stats">
    <div class="stat" onclick="showTab('diagrams')"><div class="stat-value">{s['endpoints']}</div><div class="stat-label">Endpoints</div></div>
    <div class="stat" onclick="showTab('knowledge')"><div class="stat-value">{s['streams']}</div><div class="stat-label">Streams</div></div>
    <div class="stat" onclick="showTab('diagrams')"><div class="stat-value">{s['diagrams']}</div><div class="stat-label">Diagrams</div></div>
    <div class="stat" onclick="showTab('knowledge')"><div class="stat-value">{s['authChains']}</div><div class="stat-label">Auth Chains</div></div>
    <div class="stat" onclick="showTab('knowledge')"><div class="stat-value">{s['captures']}</div><div class="stat-label">Captures</div></div>
    <div class="stat" onclick="showTab('cookies')" style="{'display:block' if s.get('cookies', 0) > 0 or p.get('targetType') == 'web' else 'display:none'}"><div class="stat-value">{s.get('cookies', 0)}</div><div class="stat-label">Cookies</div></div>
    {prompt_stat}
    <div class="stat" onclick="showTab('nextsteps')"><div class="stat-value">{len(p['nextSteps'])}</div><div class="stat-label">Next Steps</div></div>
  </div>
  <div class="tabs">
    <div class="tab active" data-tab="diagrams" onclick="showTab('diagrams')">Diagrams</div>
    <div class="tab" data-tab="narrative" onclick="showTab('narrative')">Narrative</div>
    <div class="tab" data-tab="knowledge" onclick="showTab('knowledge')">Knowledge</div>
    <div class="tab" data-tab="openapi" onclick="showTab('openapi')">OpenAPI</div>
    {"" if p.get('targetType') != 'web' else '<div class="tab" data-tab="cookies" onclick="showTab(\'cookies\')">Cookies</div>'}
    {prompt_tab}
    <div class="tab" data-tab="nextsteps" onclick="showTab('nextsteps')">Next Steps</div>
  </div>
  <div id="p{i}-diagrams" class="tab-content active">{content['diagrams']}</div>
  <div id="p{i}-narrative" class="tab-content"><div class="narrative" id="narrative-{i}"></div></div>
  <div id="p{i}-knowledge" class="tab-content">{content['knowledge']}</div>
  <div id="p{i}-openapi" class="tab-content">
    <div style="margin-bottom:16px;display:flex;gap:8px;">
      <button onclick="toggleSwagger('yaml',{i})" id="btn-yaml-{i}" class="swagger-btn active-btn">YAML Source</button>
      <button onclick="toggleSwagger('swagger',{i})" id="btn-swagger-{i}" class="swagger-btn">Swagger UI</button>
    </div>
    <div id="swagger-{i}" style="display:none;background:white;border-radius:8px;min-height:400px;"></div>
    <div id="yaml-{i}" class="openapi"><pre><code class="language-yaml">{content['openapi']}</code></pre></div>
  </div>
  <div id="p{i}-cookies" class="tab-content">{content.get('cookies', '')}</div>
  <div id="p{i}-prompt" class="tab-content">{content.get('prompt', '')}</div>
  <div id="p{i}-nextsteps" class="tab-content"><h2>Further Investigation / Next Steps</h2>{content['nextsteps']}</div>
</div>
'''

    # Narrative + prompt data for JS
    narrative_data = json.dumps({i: p['narrative'] for i, p in enumerate(projects)})
    prompt_data = json.dumps({i: p['clientPrompt'] for i, p in enumerate(projects) if p['clientPrompt']})

    page = f'''<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>IAET Investigation Dashboard</title>
<script src="https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
<link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11/build/styles/github-dark.min.css">
<script src="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11/build/highlight.min.js"></script>
<script src="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11/build/languages/json.min.js"></script>
<script src="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11/build/languages/yaml.min.js"></script>
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css">
<script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
<script src="https://cdn.jsdelivr.net/npm/js-yaml@4/dist/js-yaml.min.js"></script>
<style>
  :root {{ --bg:#0f172a;--card:#1e293b;--border:#334155;--text:#e2e8f0;--muted:#94a3b8;--accent:#6366f1;--green:#22c55e;--amber:#f59e0b; }}
  *{{margin:0;padding:0;box-sizing:border-box}}
  body{{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;background:var(--bg);color:var(--text);line-height:1.6}}
  .container{{max-width:1200px;margin:0 auto;padding:24px}}
  h1{{color:white;font-size:28px;margin-bottom:8px}}
  h2{{color:var(--accent);font-size:20px;margin:24px 0 16px;border-bottom:1px solid var(--border);padding-bottom:8px}}
  h3{{color:white;font-size:16px;margin:16px 0 12px}}
  .subtitle{{color:var(--muted);margin-bottom:16px}}
  .tabs{{display:flex;gap:0;border-bottom:2px solid var(--border);margin-bottom:24px;flex-wrap:wrap}}
  .tab{{padding:10px 20px;cursor:pointer;color:var(--muted);border-bottom:2px solid transparent;margin-bottom:-2px;font-size:14px;user-select:none}}
  .tab.active{{color:var(--accent);border-bottom-color:var(--accent)}}
  .tab:hover{{color:white}}
  .tab-content{{display:none}}.tab-content.active{{display:block}}
  .stats{{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:12px;margin-bottom:24px}}
  .stat{{background:var(--card);border-radius:8px;padding:16px;text-align:center;cursor:pointer;transition:border-color .2s;border:1px solid transparent}}
  .stat:hover{{border-color:var(--accent)}}
  .stat-value{{font-size:28px;font-weight:bold;color:var(--accent)}}.stat-label{{font-size:12px;color:var(--muted)}}
  .diagram-card,.data-card{{background:var(--card);border-radius:8px;padding:20px;margin-bottom:16px;overflow-x:auto}}
  pre.mermaid {{ background: white; border-radius: 6px; padding: 16px; margin: 12px 0; color: #333; }}
  details{{margin-top:8px}} summary{{cursor:pointer;color:var(--muted);font-size:13px}}
  pre{{background:#0d1117;border-radius:6px;padding:16px;overflow-x:auto;font-size:13px;margin-top:8px}}
  code{{font-family:"Cascadia Code","Fira Code",monospace}}
  .narrative{{background:var(--card);border-radius:8px;padding:24px}}
  .narrative h1,.narrative h2,.narrative h3{{color:white;margin-top:20px}}
  .narrative table{{width:100%;border-collapse:collapse;margin:12px 0}}
  .narrative th,.narrative td{{padding:8px 12px;border:1px solid var(--border);text-align:left;font-size:13px}}
  .narrative th{{background:var(--card);color:var(--accent)}}
  .narrative code{{background:#0d1117;padding:2px 6px;border-radius:3px;font-size:12px}}
  .narrative blockquote{{border-left:3px solid var(--accent);padding-left:12px;color:var(--muted);margin:12px 0}}
  .openapi{{background:var(--card);border-radius:8px;padding:20px}}
  .steps-group{{display:flex;flex-direction:column;gap:8px;margin-bottom:20px}}
  .step-card{{background:var(--card);border-radius:8px;padding:14px 18px;border-left:3px solid var(--amber)}}
  .step-header{{font-size:14px;margin-bottom:6px}}
  .step-header code{{background:#0d1117;padding:2px 6px;border-radius:3px;font-size:12px}}
  .step-detail{{font-size:13px;color:var(--muted)}}
  .step-detail code{{background:#0d1117;padding:2px 6px;border-radius:3px;font-size:12px;color:var(--text)}}
  .badge-tooling{{background:var(--accent);color:white;padding:2px 8px;border-radius:10px;font-size:11px;margin-left:8px}}
  .badge-human{{background:var(--amber);color:#1e293b;padding:2px 8px;border-radius:10px;font-size:11px;margin-left:8px}}
  .swagger-btn{{border:1px solid var(--border);padding:8px 16px;border-radius:6px;cursor:pointer;font-size:13px;background:var(--card);color:var(--muted)}}
  .swagger-btn.active-btn{{background:var(--accent);color:white;border:none}}
  .status-bar{{display:flex;align-items:center;gap:12px;margin-bottom:16px;padding:10px 16px;background:var(--card);border-radius:8px;border:1px solid var(--border)}}
  .status-badge{{padding:4px 12px;border-radius:12px;font-size:12px;font-weight:600;text-transform:uppercase;letter-spacing:0.5px}}
  .status-new{{background:#334155;color:#94a3b8}}
  .status-investigating{{background:#1e3a5f;color:#60a5fa}}
  .status-complete{{background:#14532d;color:#4ade80}}
  .status-archived{{background:#3f3f46;color:#a1a1aa}}
  .status-actions{{font-size:12px;color:var(--muted)}}
  .status-actions code{{background:#0d1117;padding:2px 6px;border-radius:3px;font-size:11px;color:var(--text);cursor:pointer}}
  .status-actions code:hover{{background:var(--accent);color:white}}
</style>
</head>
<body>
<div class="container">
  <h1>IAET Investigation Dashboard</h1>
  {selector_html}
  {project_divs}
</div>
<script>
  mermaid.initialize({{startOnLoad:false,theme:'default',securityLevel:'strict'}});
  // Render diagrams for the initially visible project only
  mermaid.run({{nodes:document.querySelectorAll('#project-0 pre.mermaid')}});
  const narrativeData = {narrative_data};
  const promptData = {prompt_data};
  let currentProject = 0;

  // Render narratives
  for (const [idx, md] of Object.entries(narrativeData)) {{
    const el = document.getElementById('narrative-' + idx);
    if (el && md) el.innerHTML = marked.parse(md);
  }}
  // Render client prompts
  for (const [idx, md] of Object.entries(promptData)) {{
    const el = document.querySelector('#p' + idx + '-prompt .narrative');
    if (el && md) el.innerHTML = marked.parse(md);
  }}
  hljs.highlightAll();

  function renderMermaid(projectIdx) {{
    const nodes = document.querySelectorAll('#project-' + projectIdx + ' pre.mermaid:not([data-processed])');
    if (nodes.length) mermaid.run({{nodes}});
  }}

  function switchProject(idx) {{
    document.querySelectorAll('.project-content').forEach(el => el.style.display = 'none');
    document.getElementById('project-' + idx).style.display = 'block';
    currentProject = parseInt(idx);
    renderMermaid(idx);
  }}

  function showTab(name) {{
    const container = document.getElementById('project-' + currentProject);
    container.querySelectorAll('.tab-content').forEach(el => el.classList.remove('active'));
    container.querySelectorAll('.tab').forEach(el => el.classList.remove('active'));
    const tabContent = document.getElementById('p' + currentProject + '-' + name);
    if (tabContent) tabContent.classList.add('active');
    container.querySelector('.tab[data-tab="' + name + '"]')?.classList.add('active');
    if (name === 'diagrams') renderMermaid(currentProject);
  }}

  // Copy CLI commands to clipboard on click
  document.querySelectorAll('.status-actions code').forEach(el => {{
    el.title = 'Click to copy';
    el.addEventListener('click', () => {{
      navigator.clipboard.writeText(el.textContent).then(() => {{
        const orig = el.textContent;
        el.textContent = 'Copied!';
        setTimeout(() => {{ el.textContent = orig; }}, 1200);
      }});
    }});
  }});

  const swaggerInited = {{}};
  function toggleSwagger(mode, idx) {{
    const yamlC = document.getElementById('yaml-' + idx);
    const swagC = document.getElementById('swagger-' + idx);
    const btnY = document.getElementById('btn-yaml-' + idx);
    const btnS = document.getElementById('btn-swagger-' + idx);
    if (mode === 'swagger') {{
      yamlC.style.display='none'; swagC.style.display='block';
      btnY.classList.remove('active-btn'); btnS.classList.add('active-btn');
      if (!swaggerInited[idx]) {{
        const yamlText = yamlC.querySelector('code').textContent;
        try {{
          SwaggerUIBundle({{spec:jsyaml.load(yamlText),domNode:swagC,presets:[SwaggerUIBundle.presets.apis],layout:"BaseLayout",defaultModelsExpandDepth:1,docExpansion:"list"}});
        }} catch(e) {{ swagC.innerHTML = '<p style="padding:20px;color:red;">Failed to parse OpenAPI spec: ' + e.message + '</p>'; }}
        swaggerInited[idx] = true;
      }}
    }} else {{
      yamlC.style.display='block'; swagC.style.display='none';
      btnS.classList.remove('active-btn'); btnY.classList.add('active-btn');
    }}
  }}
</script>
</body>
</html>'''

    # Determine output path
    if len(projects) == 1:
        out_dir = os.path.join(project_dirs[0], 'output')
    else:
        out_dir = projects_root

    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, 'dashboard.html')
    with open(out_path, 'w', encoding='utf-8') as f:
        f.write(page)

    print(f"Dashboard: {out_path} ({len(page) // 1024}KB, {len(projects)} project(s))")


if __name__ == '__main__':
    main()
