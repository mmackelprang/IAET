#!/usr/bin/env python3
"""Tests for the dashboard generator — run with: python -m pytest tests/dashboard_tests.py"""

import importlib.util
import json
import os
import sys

# ---------------------------------------------------------------------------
# Import generate-dashboard.py (hyphen in filename requires importlib)
# ---------------------------------------------------------------------------
_SCRIPTS_DIR = os.path.join(os.path.dirname(__file__), '..', 'scripts')
_DASHBOARD_SCRIPT = os.path.normpath(os.path.join(_SCRIPTS_DIR, 'generate-dashboard.py'))

_spec = importlib.util.spec_from_file_location('generate_dashboard', _DASHBOARD_SCRIPT)
generate_dashboard = importlib.util.module_from_spec(_spec)  # type: ignore[arg-type]
_spec.loader.exec_module(generate_dashboard)  # type: ignore[union-attr]

render_project_content = generate_dashboard.render_project_content
load_project = generate_dashboard.load_project
load_cookies = generate_dashboard.load_cookies
_strip_cookie_values = generate_dashboard._strip_cookie_values


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def make_test_project(tmp_dir, name="test-project", diagrams=None):
    """Create a minimal project structure for testing."""
    project_dir = os.path.join(tmp_dir, name)
    os.makedirs(os.path.join(project_dir, 'output', 'diagrams'), exist_ok=True)
    os.makedirs(os.path.join(project_dir, 'knowledge'), exist_ok=True)

    # project.json
    config = {
        "name": name,
        "displayName": name,
        "targetType": "web",
        "status": "investigating",
        "entryPoints": [{"url": "https://example.com", "label": "Main"}],
        "currentRound": 0,
    }
    with open(os.path.join(project_dir, 'project.json'), 'w') as f:
        json.dump(config, f)

    # Empty knowledge
    with open(os.path.join(project_dir, 'knowledge', 'endpoints.json'), 'w') as f:
        json.dump({"endpoints": []}, f)

    # narrative
    with open(os.path.join(project_dir, 'output', 'narrative.md'), 'w') as f:
        f.write("# Test\n")

    # openapi
    with open(os.path.join(project_dir, 'output', 'api.yaml'), 'w') as f:
        f.write("openapi: '3.1.0'\n")

    # diagrams
    if diagrams:
        for fname, content in diagrams.items():
            with open(os.path.join(project_dir, 'output', 'diagrams', fname), 'w') as f:
                f.write(content)

    return project_dir


# ---------------------------------------------------------------------------
# Mermaid embedding tests
# ---------------------------------------------------------------------------

class TestMermaidEmbedding:
    def test_diagrams_use_pre_mermaid_tag(self, tmp_path):
        """Mermaid content must be in <pre class="mermaid"> not <div>."""
        project_dir = make_test_project(str(tmp_path), diagrams={
            "test.mmd": "flowchart TD\n    A --> B"
        })
        project = load_project(project_dir)
        content = render_project_content(project)
        assert '<pre class="mermaid">' in content['diagrams']
        assert '<div class="mermaid">' not in content['diagrams']

    def test_br_tags_in_diagrams_are_html_escaped(self, tmp_path):
        """<br/> in Mermaid node labels must be HTML-escaped in <pre> tags."""
        project_dir = make_test_project(str(tmp_path), diagrams={
            "test.mmd": 'flowchart TD\n    A[Line 1<br/>Line 2] --> B'
        })
        project = load_project(project_dir)
        content = render_project_content(project)
        # In <pre>, the <br/> should be escaped so browser doesn't interpret it
        assert '&lt;br/&gt;' in content['diagrams']
        # Raw <br/> should NOT appear inside the <pre class="mermaid"> block
        assert '<br/>' not in content['diagrams'].split('View Source')[0]

    def test_special_chars_in_diagrams_are_escaped(self, tmp_path):
        """Special HTML chars in diagram content must be escaped."""
        project_dir = make_test_project(str(tmp_path), diagrams={
            "test.mmd": 'flowchart TD\n    A[Label with "quotes" & ampersand] --> B'
        })
        project = load_project(project_dir)
        content = render_project_content(project)
        assert '&amp;' in content['diagrams']
        assert '&quot;' in content['diagrams']

    def test_view_source_also_escaped(self, tmp_path):
        """The View Source section should also have escaped content."""
        project_dir = make_test_project(str(tmp_path), diagrams={
            "test.mmd": 'flowchart TD\n    A --> B'
        })
        project = load_project(project_dir)
        content = render_project_content(project)
        assert 'View Source' in content['diagrams']


# ---------------------------------------------------------------------------
# Cookie stripping tests
# ---------------------------------------------------------------------------

class TestCookieStripping:
    def test_cookie_values_never_in_output(self):
        """Cookie values must NEVER appear in stripped output."""
        cookies = [{"name": "SID", "value": "super_secret_123", "domain": ".google.com", "path": "/"}]
        stripped = _strip_cookie_values(cookies)
        assert stripped[0]['name'] == 'SID'
        assert 'value' not in stripped[0]
        assert 'super_secret_123' not in json.dumps(stripped)

    def test_cookie_metadata_preserved(self):
        """Cookie metadata (name, domain, httpOnly, etc.) should be preserved."""
        cookies = [{
            "name": "SID", "value": "secret", "domain": ".google.com",
            "path": "/", "httpOnly": True, "secure": True, "sameSite": "Lax",
        }]
        stripped = _strip_cookie_values(cookies)
        assert stripped[0]['domain'] == '.google.com'
        assert stripped[0]['httpOnly'] is True
        assert stripped[0]['secure'] is True
