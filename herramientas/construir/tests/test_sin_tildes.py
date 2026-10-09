"""BP-PP-197: ningún nombre visible lleva tildes, ñ ni otro carácter fuera de ASCII, en CADA idioma de la Identidad. Cada herramienta lo exige
SIN tocar la red. Las descripciones son prosa y sí pueden llevarlos. Este proyecto no tiene excepciones heredadas: la lista está vacía y fijada acá."""
import copy
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import _comun  # noqa: E402
import choice_global  # noqa: E402
import clave  # noqa: E402
import perfil_columnas  # noqa: E402
import relacion  # noqa: E402
import rol  # noqa: E402
import tabla  # noqa: E402
import test_clave  # noqa: E402
import test_columnas_estandar  # noqa: E402
import test_perfil_columnas  # noqa: E402
import test_relacion  # noqa: E402
import test_rol  # noqa: E402
import test_tabla  # noqa: E402
from cliente_simulado import FabricaCentinela  # noqa: E402
from fixtures import COMPONENTE_VALIDO, IDENTIDAD_VALIDA, playbook_md  # noqa: E402

# herramienta, identidad, playbook válido, rutas de CADA nombre visible del formato. Un nombre que es un objeto por idioma se prueba idioma por idioma.
CASOS = [
    (choice_global, IDENTIDAD_VALIDA, COMPONENTE_VALIDO, [["displayname"], ["opciones", 0, "etiqueta"]]),
    (tabla, test_tabla.IDENT, test_tabla.COMPLETO, [["displayname"], ["displayname_plural"], ["primaria", "displayname"], ["columnas", 0, "displayname"]]),
    (tabla, test_columnas_estandar.IDENT, test_columnas_estandar.EMAIL, [["columnas", 0, "displayname"]]),  # el playbook columnas-estandar entra por tabla.py
    (relacion, test_relacion.IDENT, test_relacion.BASE, [["lookup", "displayname"]]),
    (clave, test_clave.IDENT, test_clave.BASE, [["displayname"]]),
    (rol, test_rol.IDENT, test_rol.BASE, [["nombre"]]),
    (perfil_columnas, test_perfil_columnas.IDENT, test_perfil_columnas.BASE, [["nombre"]]),
]


def nodo_en(obj, ruta):
    nodo = obj
    for p in ruta:
        nodo = nodo[p]
    return nodo


def rutas_por_idioma(base, rutas):
    """Cada ruta a un nombre visible; si el nombre es un objeto por idioma, una ruta por idioma (`…, "3082"`)."""
    for ruta in rutas:
        valor = nodo_en(base, ruta)
        if isinstance(valor, dict):
            for lcid in valor:
                yield ruta + [lcid]
        else:
            yield ruta


def con_sufijo(obj, ruta, sufijo):
    n = copy.deepcopy(obj)
    nodo = nodo_en(n, ruta[:-1])
    nodo[ruta[-1]] = nodo[ruta[-1]] + sufijo
    return n


def correr(herramienta, ident, datos):
    centinela = FabricaCentinela()
    with tempfile.TemporaryDirectory() as dd:
        ruta = os.path.join(dd, "playbook.md")
        with open(ruta, "w", encoding="utf-8") as f:
            f.write(playbook_md(ident, datos))
        return herramienta.construir(ruta, False, centinela) + (centinela.llamada,)


class Validador(unittest.TestCase):
    def test_acepta_ascii_y_rechaza_lo_demas_nombrando_los_caracteres(self):
        _comun.exigir_sin_tildes("KEY - CARP - Arreglo - Producto (v2) #1", "'displayname'")
        for texto, malo in (("Código", "ó"), ("Año", "ñ"), ("Ü", "Ü"), ("Cuota – numero", "–"), ("«Cuota»", "«")):
            with self.subTest(texto):
                with self.assertRaises(_comun.ErrorPlaybook) as e:
                    _comun.exigir_sin_tildes(texto, "'displayname'")
                self.assertIn("BP-PP-197", str(e.exception))
                self.assertIn(malo, str(e.exception))
                self.assertIn("'displayname'", str(e.exception))


class CadaHerramienta(unittest.TestCase):
    def test_un_nombre_visible_con_tilde_o_enie_es_error_sin_tocar_la_red_en_cada_idioma(self):
        for herramienta, ident, base, rutas in CASOS:
            for ruta in rutas_por_idioma(base, rutas):
                for sufijo in (" técnico", " año"):
                    with self.subTest(f"{herramienta.__name__} {ruta} {sufijo}"):
                        estado, _, detalle, toco_la_red = correr(herramienta, ident, con_sufijo(base, ruta, sufijo))
                        self.assertEqual(estado, "error", detalle)
                        self.assertIn("BP-PP-197", detalle)
                        self.assertFalse(toco_la_red)

    def test_el_mensaje_nombra_el_idioma_donde_esta_la_tilde(self):
        estado, _, detalle, _ = correr(tabla, test_tabla.IDENT, con_sufijo(test_tabla.COMPLETO, ["displayname", "3082"], " técnico"))
        self.assertEqual(estado, "error", detalle)
        self.assertIn("[3082]", detalle)
        estado, _, detalle, _ = correr(tabla, test_tabla.IDENT, con_sufijo(test_tabla.COMPLETO, ["displayname", "1033"], " técnico"))
        self.assertIn("[1033]", detalle)

    def test_los_playbooks_validos_de_las_pruebas_no_llevan_tildes_en_sus_nombres(self):
        """Si esta prueba falla, las demás pruebas de esa herramienta están usando un playbook que la regla rechaza."""
        for herramienta, ident, base, rutas in CASOS:
            for ruta in rutas:
                nodo = nodo_en(base, ruta)
                with self.subTest(f"{herramienta.__name__} {ruta}"):
                    for texto in (nodo.values() if isinstance(nodo, dict) else [nodo]):
                        _comun.exigir_sin_tildes(texto, str(ruta))

    def test_la_descripcion_si_puede_llevar_tildes(self):
        for herramienta, ident, base, _ in CASOS:
            if "descripcion" not in base:
                continue
            with self.subTest(herramienta.__name__):
                estado, _, detalle, _ = correr(herramienta, ident, dict(base, descripcion=base["descripcion"] + " Descripción con ñ."))
                self.assertNotIn("BP-PP-197", detalle)

    def test_las_etiquetas_de_un_si_no_tampoco_llevan_tilde_en_ningun_idioma(self):
        i = next(i for i, c in enumerate(test_tabla.COMPLETO["columnas"]) if c["tipo"] == "sino")
        for clave_etiqueta in ("etiqueta_si", "etiqueta_no"):
            for lcid in ("1033", "3082"):
                with self.subTest(f"{clave_etiqueta} {lcid}"):
                    datos = copy.deepcopy(test_tabla.COMPLETO)
                    datos["columnas"][i][clave_etiqueta][lcid] = "Sí"
                    estado, _, detalle, toco_la_red = correr(tabla, test_tabla.IDENT, datos)
                    self.assertEqual(estado, "error", detalle)
                    self.assertIn("BP-PP-197", detalle)
                    self.assertFalse(toco_la_red)

    def test_los_motivos_de_estado_tampoco_llevan_tilde(self):
        datos = dict(copy.deepcopy(test_tabla.COMPLETO), motivos_estado=[
            {"statecode": 0, "valor": 1, "origen": "defecto", "etiqueta": {"1033": "Open", "3082": "Abierta"}, "descripcion": ""},
            {"statecode": 1, "valor": 2, "origen": "defecto", "etiqueta": {"1033": "Done", "3082": "Finalizada"}, "descripcion": ""}])
        datos["motivos_estado"][1]["etiqueta"]["3082"] = "Finalización"
        estado, _, detalle, toco_la_red = correr(tabla, test_tabla.IDENT, datos)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("BP-PP-197", detalle)
        self.assertFalse(toco_la_red)


class SinExcepciones(unittest.TestCase):
    """Este proyecto no hereda excepciones de nombres (el proyecto anterior tenía siete, ya construidas con tilde y no renombrables por el Web API). La
    lista está FIJADA acá a propósito: una excepción nueva no se agrega editando un JSON, hay que cambiar esta prueba, y eso obliga a pasar por el aprobador."""

    def test_las_listas_estan_vacias(self):
        self.assertEqual([], _comun.EXCEPCIONES_NOMBRES["nombres_visibles"])
        self.assertEqual({}, _comun.EXCEPCIONES_NOMBRES["optionset_si_no"])

    def test_sin_excepciones_todo_nombre_con_tilde_se_rechaza_incluidos_los_que_el_proyecto_anterior_exceptuaba(self):
        for nombre in ("KEY - CARP - Arreglo - Número de producto", "KEY - CARP - Conversación - Huella del Message-ID", "KEY - CARP - Versión de plantilla - Número",
                       "Requiere revisión", "KEY - MPPP - Plan - Código"):
            with self.subTest(nombre):
                with self.assertRaises(_comun.ErrorPlaybook):
                    _comun.exigir_sin_tildes(nombre, "'displayname'")

    def test_la_herramienta_de_claves_rechaza_el_nombre_con_tilde(self):
        datos = dict(test_clave.BASE, displayname={"1033": "KEY - CARP - Conversación - Huella", "3082": "KEY - CARP - Conversacion - Huella"})
        estado, _, detalle, toco_la_red = correr(clave, test_clave.IDENT, datos)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("BP-PP-197", detalle)
        self.assertFalse(toco_la_red)

    def test_el_mecanismo_de_excepcion_existe_y_se_prueba_con_una_de_ejemplo(self):
        import unittest.mock as mock
        with mock.patch.dict(_comun.EXCEPCIONES_NOMBRES, {"nombres_visibles": ["KEY - CARP - Conversación - Huella"]}):
            _comun.exigir_sin_tildes("KEY - CARP - Conversación - Huella", "'displayname'")
            with self.assertRaises(_comun.ErrorPlaybook):
                _comun.exigir_sin_tildes("KEY - CARP - Conversación - Huellas", "'displayname'")


if __name__ == "__main__":
    unittest.main()
