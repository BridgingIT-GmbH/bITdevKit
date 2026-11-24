"""Regression checks for pairing feature icons with their labels."""

import importlib.util
from pathlib import Path
import re
import unittest


spec = importlib.util.spec_from_file_location("feature_icons", Path(__file__).parents[1] / "feature_icons.py")
icons = importlib.util.module_from_spec(spec)
spec.loader.exec_module(icons)


class FeatureNameTests(unittest.TestCase):
    def setUp(self):
        icons.feature_aliases = {
            "pipeline": "pipelines",
            "orchestration": "orchestrations",
            "requester": "requester",
            "notifier": "notifier",
            "jobs": "jobs",
            "startup tasks": "startup-tasks",
            "domain": "domain",
            "domain events": "domain-events",
        }
        icons.drawings = {icon_id: '<svg aria-hidden="true"></svg>' for icon_id in icons.feature_aliases.values()}

    def test_comparison_title_pairs_each_icon_with_its_name(self):
        original = '<h1 id="pipeline-or-orchestration">' + icons.icon_html("pipelines") + icons.icon_html("orchestrations") + 'Pipeline or Orchestration<a class="headerlink" href="#pipeline-or-orchestration">¶</a></h1>'
        result = icons.decorate_feature_names(original)
        self.assertEqual(re.findall(r'data-feature-name="([^"]+)"', result), ["pipelines", "orchestrations"])
        self.assertIn('</span> or <span class="bdk-feature-name"', result)
        self.assertIn('id="pipeline-or-orchestration"', result)
        self.assertIn('href="#pipeline-or-orchestration"', result)

    def test_icon_follows_the_unrelated_words_in_a_title(self):
        result = icons.feature_label_html('Direct Call or Requester', ["requester"])
        self.assertTrue(result.startswith('Direct Call or <span class="bdk-feature-name"'))

    def test_navigation_labels_keep_their_container(self):
        result = icons.feature_label_html('<span class="md-ellipsis">Requester and Notifier</span>', ["requester", "notifier"])
        self.assertTrue(result.startswith('<span class="md-ellipsis"><span class="bdk-feature-name"'))
        self.assertEqual(re.findall(r'data-feature-icon="([^"]+)"', result), ["requester", "notifier"])

    def test_choice_headings_use_the_feature_named_in_the_heading(self):
        result = icons.decorate_feature_names('<h2>Choose Startup Tasks when</h2><h2>Choose Jobs when</h2>')
        self.assertEqual(re.findall(r'data-feature-name="([^"]+)"', result), ["startup-tasks", "jobs"])
        self.assertIn('>Choose <span class="bdk-feature-name"', result)

    def test_existing_icons_are_not_duplicated(self):
        original = '<p>' + icons.icon_html("requester") + ' Requester and Notifier</p>'
        result = icons.decorate_feature_names(original)
        self.assertEqual(result.count('data-feature-icon="requester"'), 1)
        self.assertEqual(result.count('data-feature-icon="notifier"'), 1)
        self.assertEqual(icons.decorate_feature_names(result), result)

    def test_longest_feature_name_wins(self):
        result = icons.decorate_feature_names('<p>Domain Events coordinate reactions.</p>')
        self.assertIn('data-feature-name="domain-events"', result)
        self.assertNotIn('data-feature-name="domain"', result)

    def test_existing_icon_is_not_repeated_inside_a_longer_label(self):
        original = '<p>' + icons.icon_html("domain") + '<a href="/domain/">Core Domain</a></p>'
        result = icons.decorate_feature_names(original)
        self.assertEqual(result.count('data-feature-icon="domain"'), 1)

    def test_code_prompts_attributes_and_diagrams_are_preserved(self):
        fragments = [
            '<pre><code>Requester &amp; Notifier &lt;Pipeline&gt;</code></pre>',
            '<p><code>Pipeline</code></p>',
            '<details class="prompt"><summary>AI prompt</summary><p>Use Requester.</p></details>',
            '<div class="mermaid">Pipeline --> Orchestration</div>',
            '<a href="/Pipeline/" title="Requester">Read more</a>',
        ]
        for original in fragments:
            with self.subTest(original=original):
                self.assertEqual(icons.decorate_feature_names(original), original)

    def test_generic_prose_uses_icons_only_for_named_features(self):
        result = icons.decorate_feature_names('<p>A pipeline coordinates work. Requester dispatches requests.</p>')
        self.assertEqual(re.findall(r'data-feature-name="([^"]+)"', result), ["requester"])

    def test_general_headings_can_keep_plain_text_while_feature_links_get_icons(self):
        heading = '<h2 id="model-a-domain" data-feature-icons="false">Model a domain</h2>'
        result = icons.decorate_feature_names(heading + '<p><a href="/domain/">Domain</a></p>')
        self.assertTrue(result.startswith(heading))
        self.assertEqual(result.count('data-feature-icon="domain"'), 1)
        self.assertIn('data-feature-name="domain"', result)

    def test_topic_navigation_can_keep_plain_text(self):
        navigation = '<div data-feature-icons="false"><a href="#domain">Model a domain</a></div>'
        self.assertEqual(icons.decorate_feature_names(navigation), navigation)

    def test_labels_and_table_headings_accept_lowercase_feature_names(self):
        result = icons.decorate_feature_names('<a href="/pipeline/">pipeline</a><table><tr><th>orchestration</th></tr></table>')
        self.assertEqual(re.findall(r'data-feature-name="([^"]+)"', result), ["pipelines", "orchestrations"])

    def test_long_names_can_wrap_after_the_first_word(self):
        result = icons.feature_label_html('Startup Tasks', ["startup-tasks"])
        self.assertIn('Startup</span> Tasks', result)

    def test_generic_titles_keep_one_representative_icon(self):
        result = icons.feature_label_html(icons.icon_html("domain") + 'Where to Put Business Logic')
        self.assertEqual(result.count('data-feature-icon="domain"'), 1)
        self.assertTrue(result.endswith('Where to Put Business Logic'))


if __name__ == "__main__":
    unittest.main()
