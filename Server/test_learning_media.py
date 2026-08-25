from pathlib import Path
import unittest


HTML = Path(__file__).resolve().parent / "public" / "learning-media.html"


def read_html():
    return HTML.read_text(encoding="utf-8")


class LearningMediaPageTests(unittest.TestCase):
    def test_total_data_bahan_label_is_exactly_100_bahan(self):
        html = read_html()

        self.assertIn('totalEntry.textContent = "100 Bahan";', html)


    def test_internal_video_is_visible_in_main_learning_material_panel(self):
        html = read_html()
        msds_panel_start = html.index('<article id="panel-msds"')
        msds_panel_end = html.index('</article>', msds_panel_start)
        msds_panel = html[msds_panel_start:msds_panel_end]

        self.assertIn('class="media-feature-video info-card"', msds_panel)
        self.assertIn('assets/videos/0314.mp4', msds_panel)
        self.assertIn('<video', msds_panel)


    def test_k3lh_presentation_material_is_listed(self):
        html = read_html()

        self.assertIn('Persentasi K3LH', html)
        self.assertIn('assets/materials/persentasi-K3LH.pdf', html)
        self.assertIn('Lihat PDF K3LH', html)
        self.assertIn('Download PDF K3LH', html)


if __name__ == "__main__":
    unittest.main()
