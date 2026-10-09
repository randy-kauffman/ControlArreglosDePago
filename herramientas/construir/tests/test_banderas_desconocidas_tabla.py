"""`tabla.py` frente a una bandera que no conoce.

El 2026-09-25 se le pasó `--completar` —que existe en `rol.py` y en `datos.py`,
no acá— y la herramienta siguió como si nada, devolviendo `difiere`. Parecía que
se negaba a agregar columnas cuando en realidad estaba ignorando la instrucción.

Misma familia que el `ya_existia` mentiroso del ribbon: una herramienta que calla
lo que no entendió es una herramienta en la que no se puede confiar, y todo el
método de este proyecto se apoya en confiar en ellas (D-56)."""
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import tabla as tb  # noqa: E402


class BanderasDesconocidas(unittest.TestCase):
    def _correr(self, *argv):
        vistos = {}
        original_argv, original_salida = sys.argv, tb.salida
        sys.argv = ["tabla.py", *argv]
        tb.salida = lambda estado, comp, det: vistos.update(estado=estado, detalle=det) or 1
        try:
            tb.main()
        finally:
            sys.argv, tb.salida = original_argv, original_salida
        return vistos

    def test_una_bandera_que_no_existe_es_error(self):
        r = self._correr("p.md", "--completar")
        self.assertEqual(r["estado"], "error")
        self.assertIn("--completar", r["detalle"])

    def test_el_error_llega_ANTES_de_tocar_el_entorno(self):
        """No se importa el cliente de Dataverse ni se lee el playbook: falla y corta."""
        r = self._correr("no-existe-este-archivo.md", "--completar")
        self.assertEqual(r["estado"], "error")
        self.assertIn("no conoce", r["detalle"])

    def test_las_nombra_a_TODAS(self):
        r = self._correr("p.md", "--completar", "--forzar")
        self.assertIn("--completar", r["detalle"])
        self.assertIn("--forzar", r["detalle"])

    def test_las_banderas_de_verdad_no_se_tocan(self):
        for bandera in tb.BANDERAS:
            r = self._correr("no-existe-este-archivo.md", bandera)
            self.assertNotIn("no conoce", r.get("detalle", ""), f"{bandera} quedó fuera de BANDERAS")


if __name__ == "__main__":
    unittest.main()
