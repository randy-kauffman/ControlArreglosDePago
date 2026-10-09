"""Comparación de una relación contra el XML de la solución exportada. Usa las
muestras de `tests/muestras/relacion/` (formas de exportación derivadas del
proyecto anterior: ver su `LEEME.txt`)."""
import copy
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, os.path.dirname(os.path.dirname(_AQUI)))
sys.path.insert(0, _AQUI)

import muestra_relacion as mr  # noqa: E402

M = os.path.join(_AQUI, "muestras", "relacion")
IDIOMAS = [1033, 3082]


def leer(nombre):
    with open(os.path.join(M, nombre), encoding="utf-8") as f:
        return f.read()


def armar_xml(relaciones=None):
    def entidad(nombre, atributos):
        return (f"<Entity><Name>{nombre}</Name><EntityInfo><entity Name=\"{nombre}\"><attributes>{atributos}</attributes></entity></EntityInfo></Entity>")

    return ("<ImportExportXml><Entities>"
            + entidad("sanic_carp_tbl_arreglo", leer("arreglo.lookups.solucion.xml"))
            + entidad("sanic_carp_tbl_conversacion", leer("conversacion.lookup.solucion.xml"))
            + "</Entities>" + (relaciones if relaciones is not None else leer("relaciones.solucion.xml")) + "</ImportExportXml>")


XML = armar_xml()

# Los tres datos salen del diccionario (§6.2): REL-04 (comun, requerida y auditada), REL-12 (hacia `systemuser`) y REL-09 (autorreferida).
VERSION = {"tipo": "relacion", "nombre": "sanic_carp_versionplantilla_arreglo", "tabla_padre": "sanic_carp_tbl_versionplantilla",
           "tabla_hija": "sanic_carp_tbl_arreglo", "comportamiento": "restringido",
           "lookup": {"nombre": "sanic_versionplantillaid", "displayname": {"1033": "Template version", "3082": "Version de plantilla"},
                      "descripcion": "Versión de la plantilla con la que se armó el arreglo.", "requerida": True, "auditoria": True}}
OFICIAL = {"tipo": "relacion", "nombre": "sanic_carp_systemuser_arreglo_oficialid", "tabla_padre": "systemuser",
           "tabla_hija": "sanic_carp_tbl_arreglo", "comportamiento": "restringido",
           "lookup": {"nombre": "sanic_oficialid", "displayname": {"1033": "Oficial", "3082": "Oficial"},
                      "descripcion": "Oficial de crédito que atiende el arreglo.", "requerida": True, "auditoria": False}}
UNIDA = {"tipo": "relacion", "nombre": "sanic_carp_conversacion_conversacion", "tabla_padre": "sanic_carp_tbl_conversacion",
         "tabla_hija": "sanic_carp_tbl_conversacion", "jerarquica": False, "comportamiento": "quitar_vinculo",
         "lookup": {"nombre": "sanic_unidaaid", "displayname": {"1033": "Unida a", "3082": "Unida a"},
                    "descripcion": "Conversación a la que esta fue unida.", "requerida": False, "auditoria": True}}
TODAS = (VERSION, OFICIAL, UNIDA)


def cambiar(d, ruta, valor):
    n = copy.deepcopy(d)
    nodo = n
    for p in ruta[:-1]:
        nodo = nodo[p]
    nodo[ruta[-1]] = valor
    return n


class ContraLaMuestra(unittest.TestCase):
    def test_coincide(self):
        for d in TODAS:
            with self.subTest(d["nombre"]):
                self.assertEqual(mr.comparar_con_xml(XML, d, IDIOMAS), [])

    def test_una_tabla_del_sistema_viene_con_su_nombre_de_esquema(self):
        """El XML exportado nombra las tablas por su nombre de esquema (`SystemUser`);
        el playbook, por el lógico (`systemuser`). No es una diferencia."""
        difs = mr.comparar_con_xml(XML, OFICIAL, IDIOMAS)
        self.assertFalse([d for d in difs if d.startswith("tabla_padre")], difs)
        otra = mr.comparar_con_xml(XML, cambiar(OFICIAL, ["tabla_padre"], "team"), IDIOMAS)
        self.assertTrue([d for d in otra if d.startswith("tabla_padre")], otra)

    def test_no_esta(self):
        difs = mr.comparar_con_xml(XML, cambiar(VERSION, ["nombre"], "sanic_carp_versionplantilla_cuota"), IDIOMAS)
        self.assertEqual(len(difs), 1)
        self.assertIn("no está en la solución exportada", difs[0])

    def test_detecta_cada_diferencia(self):
        casos = [("tabla_padre", cambiar(VERSION, ["tabla_padre"], "sanic_carp_tbl_cliente")),
                 ("tabla_hija", cambiar(VERSION, ["tabla_hija"], "sanic_carp_tbl_cuota")),
                 ("comportamiento", cambiar(VERSION, ["comportamiento"], "parental")),
                 ("comportamiento", cambiar(VERSION, ["comportamiento"], "quitar_vinculo")),
                 ("lookup.requerida", cambiar(VERSION, ["lookup", "requerida"], False)),
                 ("lookup.auditoria", cambiar(VERSION, ["lookup", "auditoria"], False)),
                 ("lookup.displayname[1033]", cambiar(VERSION, ["lookup", "displayname", "1033"], "Otro")),
                 ("lookup.displayname[3082]", cambiar(VERSION, ["lookup", "displayname", "3082"], "Otro")),
                 ("lookup.descripcion[1033]", cambiar(VERSION, ["lookup", "descripcion"], "Otra.")),
                 ("lookup.descripcion[3082]", cambiar(VERSION, ["lookup", "descripcion"], "Otra.")),
                 ("falta la columna lookup", cambiar(VERSION, ["lookup", "nombre"], "sanic_inexistenteid"))]
        for esperado, datos in casos:
            with self.subTest(esperado, comportamiento=datos["comportamiento"]):
                difs = mr.comparar_con_xml(XML, datos, IDIOMAS)
                self.assertTrue(any(esperado in d for d in difs), difs)

    def test_las_etiquetas_se_comparan_en_los_dos_sentidos(self):
        """Un idioma que el playbook declara y el XML no trae, y un idioma que el XML trae y el playbook no declara."""
        sin_3082 = mr.comparar_con_xml(XML, VERSION, [1033])
        self.assertTrue(any("lookup.displayname" in d and "3082" in d and "no declara" in d for d in sin_3082), sin_3082)
        self.assertTrue(any("lookup.descripcion" in d and "3082" in d and "no declara" in d for d in sin_3082), sin_3082)
        de_mas = mr.comparar_con_xml(XML, cambiar(VERSION, ["lookup", "displayname", "1034"], "Plantilla"), [1033, 3082, 1034])
        self.assertTrue(any("lookup.displayname[1034]" in d for d in de_mas), de_mas)
        self.assertTrue(any("lookup.descripcion[1034]" in d for d in de_mas), de_mas)

    def test_la_descripcion_se_compara_con_el_mismo_texto_en_cada_idioma(self):
        xml = armar_xml().replace('description="Oficial de crédito que atiende el arreglo." languagecode="3082"', 'description="Otro texto." languagecode="3082"', 1)
        difs = mr.comparar_con_xml(xml, OFICIAL, IDIOMAS)
        self.assertTrue(any("lookup.descripcion[3082]" in d for d in difs), difs)
        self.assertFalse(any("lookup.descripcion[1033]" in d for d in difs), difs)


class Jerarquia(unittest.TestCase):
    """El formato nunca cubre una relación jerárquica: `IsHierarchical` del XML tiene que ser 0 (en la autorreferida y en cualquier otra)."""

    @staticmethod
    def _con_jerarquica(nombre):
        """El XML de la solución con `IsHierarchical` = 1 solo en la relación `nombre`."""
        relaciones = leer("relaciones.solucion.xml")
        ini = relaciones.index(f'<EntityRelationship Name="{nombre}">')
        fin = relaciones.index("</EntityRelationship>", ini)
        bloque = relaciones[ini:fin]
        assert "<IsHierarchical>0</IsHierarchical>" in bloque
        return armar_xml(relaciones[:ini] + bloque.replace("<IsHierarchical>0</IsHierarchical>", "<IsHierarchical>1</IsHierarchical>") + relaciones[fin:])

    def test_una_relacion_jerarquica_difiere_aunque_sea_la_autorreferida(self):
        for d in TODAS:
            with self.subTest(d["nombre"]):
                difs = mr.comparar_con_xml(self._con_jerarquica(d["nombre"]), d, IDIOMAS)
                self.assertTrue(any(x.startswith("IsHierarchical") for x in difs), difs)

    def test_la_autorreferida_nombra_la_misma_tabla_de_los_dos_lados(self):
        """`ReferencedEntityName` igual a `ReferencingEntityName` y a la tabla del playbook."""
        xml = armar_xml(leer("relaciones.solucion.xml").replace("<ReferencedEntityName>sanic_carp_tbl_conversacion", "<ReferencedEntityName>sanic_carp_tbl_cuota"))
        difs = mr.comparar_con_xml(xml, UNIDA, IDIOMAS)
        self.assertTrue(any(x.startswith("tabla_padre") for x in difs), difs)
        otra_hija = mr.comparar_con_xml(armar_xml(), cambiar(UNIDA, ["tabla_hija"], "sanic_carp_tbl_cuota"), IDIOMAS)
        self.assertTrue(any(x.startswith("tabla_hija") for x in otra_hija), otra_hija)


class CarpetaDePlaybooks(unittest.TestCase):
    def test_sale_de_la_configuracion_del_proyecto(self):
        """La carpeta no está escrita en el código: se lee de la clave `playbooks` de `proyecto.json`."""
        ruta = mr.carpeta_playbooks({"playbooks": "otra/carpeta"})
        self.assertEqual(ruta, os.path.join(mr.RAIZ_PROYECTO, "otra", "carpeta", "relacion"))

    def test_sin_configuracion_explicita_usa_proyecto_json(self):
        from credenciales import cargar_configuracion

        self.assertEqual(mr.carpeta_playbooks(), os.path.join(mr.RAIZ_PROYECTO, cargar_configuracion()["playbooks"], "relacion"))


if __name__ == "__main__":
    unittest.main()
