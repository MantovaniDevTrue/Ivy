import importlib.util
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT=Path(__file__).parents[1]
spec=importlib.util.spec_from_file_location('pack',ROOT/'scripts/pack_game.py')
pack=importlib.util.module_from_spec(spec);spec.loader.exec_module(pack)
class PanelResourcesTests(unittest.TestCase):
 def test_only_ivy_resources_are_merged(self):
  with tempfile.TemporaryDirectory() as directory:
   base=Path(directory)/'base';host=Path(directory)/'host'
   (base/'res/values').mkdir(parents=True);(host/'res/values').mkdir(parents=True)
   (base/'res/values/game.xml').write_text('<resources><string name="game_title">Terraria</string></resources>')
   (host/'res/values/strings.xml').write_text('<resources><string name="ivy_title">Ivy</string><string name="other">Do not merge</string></resources>')
   (host/'res/values/public.xml').write_text('<resources><public name="ivy_title" type="string" id="0x7f000001"/></resources>')
   (host/'res/layout').mkdir();(host/'res/layout/ivy_panel.xml').write_text('<FrameLayout/>')
   pack.merge_panel_resources(base,host)
   self.assertEqual((base/'res/values/game.xml').read_text(),'<resources><string name="game_title">Terraria</string></resources>')
   merged=ET.parse(base/'res/values/ivy_strings.xml').getroot()
   self.assertEqual([n.get('name')for n in merged],['ivy_title'])
   self.assertFalse((base/'res/values/ivy_public.xml').exists())
   self.assertTrue((base/'res/layout/ivy_panel.xml').exists())
 def test_conflicting_ivy_layout_is_rejected(self):
  with tempfile.TemporaryDirectory()as directory:
   base=Path(directory)/'base';host=Path(directory)/'host'
   for p in(base,host):(p/'res/layout').mkdir(parents=True)
   (base/'res/layout/ivy_panel.xml').write_text('old');(host/'res/layout/ivy_panel.xml').write_text('new')
   with self.assertRaises(ValueError):pack.merge_panel_resources(base,host)
