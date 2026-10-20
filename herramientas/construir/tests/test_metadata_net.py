"""Pruebas de `_metadata_net.py`: el lector de los tipos definidos en un ensamblado .NET.
Usan el ensamblado mínimo de `dll_de_mentira.py` (metadata de verdad, sin compilador)."""
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import _metadata_net as mn  # noqa: E402
from dll_de_mentira import dll_dotnet  # noqa: E402

TIPOS = ["Sanic.Carp.Plugins.Api.GenerarFormatoApi", "Sanic.Carp.Plugins.Steps.VincularCorreoStep"]


class NombresDeTipos(unittest.TestCase):
    def test_devuelve_el_nombre_completo_con_namespace(self):
        nombres = mn.nombres_de_tipos(dll_dotnet(TIPOS))
        self.assertEqual({"<Module>", *TIPOS}, nombres)

    def test_el_ultimo_tramo_solo_no_alcanza(self):
        # Otro namespace, misma clase: el nombre completo es lo que distingue.
        nombres = mn.nombres_de_tipos(dll_dotnet(["Otro.Namespace.GenerarFormatoApi"]))
        self.assertNotIn("Sanic.Carp.Plugins.Api.GenerarFormatoApi", nombres)
        self.assertIn("Otro.Namespace.GenerarFormatoApi", nombres)

    def test_un_tipo_sin_namespace_figura_solo_con_su_nombre(self):
        self.assertIn("Suelta", mn.nombres_de_tipos(dll_dotnet(["Suelta"])))

    def test_acepta_nombres_con_caracteres_fuera_de_ascii(self):
        self.assertIn("Sanic.Ñandú.Clase", mn.nombres_de_tipos(dll_dotnet(["Sanic.Ñandú.Clase"])))

    def test_lo_que_no_es_un_ensamblado_net_se_rechaza_con_su_error(self):
        casos = {
            "bytes cualquiera": b"esto no es un PE",
            "vacío": b"",
            "PE truncado": dll_dotnet(TIPOS)[:100],
            "metadata truncada": dll_dotnet(TIPOS)[:0x240],
        }
        for nombre, crudo in casos.items():
            with self.subTest(nombre):
                with self.assertRaises(mn.NoEsEnsambladoNet):
                    mn.nombres_de_tipos(crudo)

    def test_un_ejecutable_nativo_sin_cabecera_cli_se_rechaza(self):
        nativo = bytearray(dll_dotnet(TIPOS))
        # El directorio de datos 14 (cabecera CLI) en cero: un PE nativo.
        nativo[64 + 4 + 20 + 96 + 14 * 8: 64 + 4 + 20 + 96 + 14 * 8 + 8] = b"\x00" * 8
        with self.assertRaises(mn.NoEsEnsambladoNet) as e:
            mn.nombres_de_tipos(bytes(nativo))
        self.assertIn("nativo", str(e.exception))


if __name__ == "__main__":
    unittest.main()
