"""Comparación de una relación contra el XML de la solución exportada. Usa las
muestras de `tests/muestras/relacion/` (formas de exportación derivadas del
proyecto anterior, y la exportación REAL del 2026-10-09 de la variante B, hija
estándar: ver su `LEEME.txt`)."""
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


# -- Variante B (hija estándar): exportación REAL del 2026-10-09 (Dev), con el sufijo de ensayo `zz` quitado de los nombres (ver `LEEME.txt`) ---------------------------
ENTIDAD_EMAIL = leer("email.solucion.xml")
RELACION_EMAIL = leer("sanic_carp_systemuser_email.solucion.xml")
SOLUTION_EMAIL = leer("sanic_carp_systemuser_email.solution.xml")


def armar_xml_b(entidad=ENTIDAD_EMAIL, relacion=RELACION_EMAIL):
    """El `customizations.xml` mínimo de la exportación: la entidad Email (con SOLO nuestros atributos) y la relación. Las tablas van por su nombre de esquema (`Email`)."""
    return f"<ImportExportXml><Entities>{entidad}</Entities><EntityRelationships>{relacion}</EntityRelationships></ImportExportXml>"


# Los textos son los del ensayo ("Ensayo respondido por" / "Ensayo."): la exportación real, sin tocar más que el sufijo `zz`.
EMAIL = {"tipo": "relacion", "nombre": "sanic_carp_systemuser_email", "tabla_padre": "systemuser", "tabla_hija": "email", "comportamiento": "restringido",
         "lookup": {"nombre": "sanic_respondidoporid", "displayname": {"1033": "Ensayo respondido por", "3082": "Ensayo respondido por"},
                    "descripcion": "Ensayo.", "requerida": False, "auditoria": False}}
XML_B = armar_xml_b()


class HijaEstandarContraLaMuestra(unittest.TestCase):
    """La relación `systemuser` → `email` contra la exportación real: el XML nombra las tablas por su nombre de esquema (`Email`, `SystemUser`) y el playbook por
    el lógico (`email`, `systemuser`); la entidad Email trae SOLO nuestros atributos; y `solution.xml` trae el `RootComponent` de `email` con `behavior` 1."""

    def test_la_muestra_es_la_real(self):
        self.assertIn("<ReferencingEntityName>Email</ReferencingEntityName>", RELACION_EMAIL)
        self.assertIn("<ReferencedEntityName>SystemUser</ReferencedEntityName>", RELACION_EMAIL)
        self.assertIn('<entity Name="Email">', ENTIDAD_EMAIL)
        self.assertIn('<RootComponent type="1" schemaName="email" behavior="1" />', SOLUTION_EMAIL)
        self.assertNotIn("zz", RELACION_EMAIL + ENTIDAD_EMAIL.replace("sanic_tiemporespuesta", ""))

    def test_coincide(self):
        self.assertEqual(mr.comparar_con_xml(XML_B, EMAIL, IDIOMAS, solution_xml=SOLUTION_EMAIL), [])

    def test_sin_solution_xml_no_se_mira_el_root_component(self):
        sin_root = SOLUTION_EMAIL.replace('<RootComponent type="1" schemaName="email" behavior="1" />', "")
        self.assertEqual(mr.comparar_con_xml(XML_B, EMAIL, IDIOMAS), [])
        self.assertEqual(len(mr.comparar_con_xml(XML_B, EMAIL, IDIOMAS, solution_xml=sin_root)), 1)

    def test_la_relacion_comun_no_mira_el_root_component(self):
        """El `solution.xml` solo importa para una hija estándar: la tabla propia viaja con todos sus subcomponentes y no se compara contra él."""
        self.assertEqual(mr.comparar_con_xml(XML, VERSION, IDIOMAS), [])

    def test_no_esta(self):
        difs = mr.comparar_con_xml(armar_xml_b(relacion=""), EMAIL, IDIOMAS, solution_xml=SOLUTION_EMAIL)
        self.assertEqual(difs, ["sanic_carp_systemuser_email no está en la solución exportada"])

    def test_cada_diferencia_con_el_detalle_exacto(self):
        parental = ["comportamiento (Assign): xml='NoCascade' playbook='Cascade'", "comportamiento (Delete): xml='Restrict' playbook='Cascade'",
                    "comportamiento (Reparent): xml='NoCascade' playbook='Cascade'", "comportamiento (Share): xml='NoCascade' playbook='Cascade'",
                    "comportamiento (Unshare): xml='NoCascade' playbook='Cascade'"]
        casos = [
            ("tabla_padre", cambiar(EMAIL, ["tabla_padre"], "team"), ["tabla_padre: xml='SystemUser' playbook='team'"]),
            ("tabla_hija", cambiar(EMAIL, ["tabla_hija"], "task"), ["tabla_hija: xml='Email' playbook='task'", "tabla_hija: 'task' no figura como RootComponent type=\"1\" en solution.xml",
                                                                                "falta la columna lookup sanic_respondidoporid en task"]),
            ("quitar_vinculo", cambiar(EMAIL, ["comportamiento"], "quitar_vinculo"), ["comportamiento (Delete): xml='Restrict' playbook='RemoveLink'"]),
            ("parental", cambiar(EMAIL, ["comportamiento"], "parental"), parental),
            ("requerida", cambiar(EMAIL, ["lookup", "requerida"], True), ["lookup.requerida: xml='none' playbook='required'"]),
            ("auditoria", cambiar(EMAIL, ["lookup", "auditoria"], True), ["lookup.auditoria: xml='0' playbook='1'"]),
            ("displayname 1033", cambiar(EMAIL, ["lookup", "displayname", "1033"], "Otro"), ["lookup.displayname[1033]: xml='Ensayo respondido por' playbook='Otro'"]),
            ("displayname 3082", cambiar(EMAIL, ["lookup", "displayname", "3082"], "Otro"), ["lookup.displayname[3082]: xml='Ensayo respondido por' playbook='Otro'"]),
            ("descripcion", cambiar(EMAIL, ["lookup", "descripcion"], "Otra."),
             ["lookup.descripcion[1033]: xml='Ensayo.' playbook='Otra.'", "lookup.descripcion[3082]: xml='Ensayo.' playbook='Otra.'"]),
            ("nombre del lookup", cambiar(EMAIL, ["lookup", "nombre"], "sanic_otroid"),
             ["lookup.nombre: xml='sanic_respondidoporid' playbook='sanic_otroid'", "falta la columna lookup sanic_otroid en email"]),
        ]
        for nombre, datos, esperado in casos:
            with self.subTest(nombre):
                self.assertEqual(mr.comparar_con_xml(XML_B, datos, IDIOMAS, solution_xml=SOLUTION_EMAIL), esperado)

    def test_cada_diferencia_del_xml_con_el_detalle_exacto(self):
        sin_lookup = ENTIDAD_EMAIL[:ENTIDAD_EMAIL.index('<attribute PhysicalName="sanic_respondidoporid">')] + ENTIDAD_EMAIL[ENTIDAD_EMAIL.index("</attributes>"):]
        casos = [
            ("IsHierarchical", armar_xml_b(relacion=RELACION_EMAIL.replace("<IsHierarchical>0</IsHierarchical>", "<IsHierarchical>1</IsHierarchical>")),
             ["IsHierarchical: xml='1' playbook='0'"]),
            ("CascadeDelete", armar_xml_b(relacion=RELACION_EMAIL.replace("<CascadeDelete>Restrict", "<CascadeDelete>Cascade")),
             ["comportamiento (Delete): xml='Cascade' playbook='Restrict'"]),
            ("Referenced", armar_xml_b(relacion=RELACION_EMAIL.replace("SystemUser</ReferencedEntityName>", "Team</ReferencedEntityName>")),
             ["tabla_padre: xml='Team' playbook='systemuser'"]),
            ("Referencing", armar_xml_b(relacion=RELACION_EMAIL.replace("<ReferencingEntityName>Email", "<ReferencingEntityName>Task")),
             ["tabla_hija: xml='Task' playbook='email'"]),
            ("lookup fuera de la entidad", armar_xml_b(entidad=sin_lookup), ["falta la columna lookup sanic_respondidoporid en email"]),
            ("sin la entidad", armar_xml_b(entidad=""), ["falta la columna lookup sanic_respondidoporid en email"]),
            ("el lookup no es lookup", armar_xml_b(entidad=ENTIDAD_EMAIL.replace("<Type>lookup</Type>", "<Type>int</Type>")), ["lookup.tipo: xml='int' playbook='lookup'"]),
            ("seguridad de columna", armar_xml_b(entidad=ENTIDAD_EMAIL.replace("<IsSecured>0</IsSecured>", "<IsSecured>1</IsSecured>")), ["lookup.protegida: xml='1' playbook='0'"]),
        ]
        for nombre, xml, esperado in casos:
            with self.subTest(nombre):
                difs = mr.comparar_con_xml(xml, EMAIL, IDIOMAS, solution_xml=SOLUTION_EMAIL)
                self.assertEqual(difs, esperado)

    def test_la_tabla_estandar_en_solution_xml(self):
        root = '<RootComponent type="1" schemaName="email" behavior="1" />'
        casos = [
            ("behavior 0", SOLUTION_EMAIL.replace(root, '<RootComponent type="1" schemaName="email" behavior="0" />'),
             ["tabla_hija: RootComponent de 'email' con behavior=\"0\" en solution.xml: tiene que ser distinto de 0"]),
            ("behavior 2", SOLUTION_EMAIL.replace(root, '<RootComponent type="1" schemaName="email" behavior="2" />'), []),
            ("sin RootComponent", SOLUTION_EMAIL.replace(root, ""), ["tabla_hija: 'email' no figura como RootComponent type=\"1\" en solution.xml"]),
            ("otro tipo de componente", SOLUTION_EMAIL.replace(root, '<RootComponent type="9" schemaName="email" behavior="1" />'),
             ["tabla_hija: 'email' no figura como RootComponent type=\"1\" en solution.xml"]),
            ("otra tabla", SOLUTION_EMAIL.replace(root, '<RootComponent type="1" schemaName="task" behavior="1" />'),
             ["tabla_hija: 'email' no figura como RootComponent type=\"1\" en solution.xml"]),
            ("dos veces", SOLUTION_EMAIL.replace(root, root + root),
             ["tabla_hija: 'email' figura 2 veces como RootComponent type=\"1\" en solution.xml, se esperaba 1"]),
            ("con otra mayúscula", SOLUTION_EMAIL.replace(root, '<RootComponent type="1" schemaName="Email" behavior="1" />'), []),
        ]
        for nombre, solution, esperado in casos:
            with self.subTest(nombre):
                self.assertEqual(mr.comparar_con_xml(XML_B, EMAIL, IDIOMAS, solution_xml=solution), esperado)

    def test_las_etiquetas_se_comparan_en_los_dos_sentidos(self):
        difs = mr.comparar_con_xml(XML_B, EMAIL, [1033], solution_xml=SOLUTION_EMAIL)
        self.assertEqual(difs, ["lookup.displayname: tiene etiquetas en un idioma que el playbook no declara [3082]",
                                "lookup.descripcion: tiene etiquetas en un idioma que el playbook no declara [3082]"])

    def test_la_entidad_se_busca_sin_distinguir_mayusculas_y_la_tabla_propia_sigue_igual(self):
        self.assertEqual(mr.comparar_con_xml(XML, VERSION, IDIOMAS), [])
        self.assertEqual(mr.comparar_con_xml(XML, UNIDA, IDIOMAS), [])


class Fragmentos(unittest.TestCase):
    """Lo que `--guardar` escribe en `muestras/`: para una relación común, su `<EntityRelationship>`; para una hija estándar, además la entidad y el `solution.xml`."""
    def test_hija_estandar_guarda_la_relacion_la_entidad_y_el_solution_xml(self):
        f = mr.fragmentos_a_guardar(XML_B, SOLUTION_EMAIL, EMAIL, True)
        self.assertEqual(sorted(f), ["email.solucion.xml", "sanic_carp_systemuser_email.solucion.xml", "sanic_carp_systemuser_email.solution.xml"])
        self.assertTrue(f["sanic_carp_systemuser_email.solucion.xml"].startswith('<EntityRelationship Name="sanic_carp_systemuser_email">'))
        self.assertTrue(f["email.solucion.xml"].startswith("<Entity>"))
        self.assertIn('<attribute PhysicalName="sanic_respondidoporid">', f["email.solucion.xml"])
        self.assertEqual(f["sanic_carp_systemuser_email.solution.xml"], SOLUTION_EMAIL)
        for nombre in ("email.solucion.xml", "sanic_carp_systemuser_email.solucion.xml"):
            self.assertTrue(f[nombre].endswith("\n") and not f[nombre].endswith(" \n"), nombre)

    def test_lo_que_se_guarda_se_vuelve_a_comparar_igual(self):
        f = mr.fragmentos_a_guardar(XML_B, SOLUTION_EMAIL, EMAIL, True)
        otra = armar_xml_b(entidad=f["email.solucion.xml"], relacion=f["sanic_carp_systemuser_email.solucion.xml"])
        self.assertEqual(mr.comparar_con_xml(otra, EMAIL, IDIOMAS, solution_xml=f["sanic_carp_systemuser_email.solution.xml"]), [])

    def test_una_relacion_comun_guarda_solo_su_fragmento(self):
        f = mr.fragmentos_a_guardar(XML, SOLUTION_EMAIL, VERSION, False)
        self.assertEqual(list(f), ["sanic_carp_versionplantilla_arreglo.solucion.xml"])
        self.assertTrue(f["sanic_carp_versionplantilla_arreglo.solucion.xml"].startswith('<EntityRelationship Name="sanic_carp_versionplantilla_arreglo">'))

    def test_una_relacion_que_no_esta_no_guarda_nada(self):
        self.assertEqual(mr.fragmentos_a_guardar(XML, SOLUTION_EMAIL, cambiar(VERSION, ["nombre"], "sanic_carp_versionplantilla_cuota"), False), {})
        self.assertEqual(mr.fragmentos_a_guardar(armar_xml_b(relacion=""), SOLUTION_EMAIL, EMAIL, True), {})


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
