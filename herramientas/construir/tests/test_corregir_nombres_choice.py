"""`choice_global.py --corregir-nombres`: la única modificación que la
herramienta admite sobre un choice que ya existe. Solo nombres visibles (el
del choice y las etiquetas de sus opciones, en cualquiera de los idiomas
declarados), y solo si esa es la ÚNICA diferencia. Ensayado contra la
plataforma el 2026-09-21.

Con varios idiomas, cada escritura manda la etiqueta de TODOS los idiomas de
Identidad (con `MergeLabels`), y el reconocedor de "solo difieren nombres"
entiende los mensajes de diferencia por idioma (`DisplayName[3082]: ...`)."""
import copy
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import choice_global as cg  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402
from fixtures import (  # noqa: E402
    IDENTIDAD_VALIDA,
    armar_cliente_precondiciones_ok,
    componente,
    cuerpo_choice_conforme,
    es_ruta_solutioncomponents,
    escribir_playbook,
    identidad,
    ruta_choice,
)

# Diccionario de datos, 3.1: choice de producto crediticio. Textos distintos por idioma a propósito: así se nota si una
# escritura manda el texto de un idioma en lugar del de otro.
PRODUCTO = componente(
    nombre="sanic_carp_ch_productocrediticio",
    displayname={"1033": "CH - CARP - Credit product", "3082": "CH - CARP - Producto crediticio"},
    descripcion="Producto sobre el que se hace el arreglo: préstamo o tarjeta.",
    opciones=[
        {"valor": 159460001, "etiqueta": {"1033": "Loan", "3082": "Prestamo"}, "descripcion": "Préstamo"},
        {"valor": 159460002, "etiqueta": {"1033": "Card", "3082": "Tarjeta"}, "descripcion": "Tarjeta de crédito"},
    ],
)
NOMBRE = PRODUCTO["nombre"]
META = "11111111-1111-1111-1111-111111111111"
LOC = "LocalizedLabels"


def armar(cuerpo_viejo, ident=None):
    """El entorno arranca con `cuerpo_viejo` y refleja cada escritura de nombres (de TODOS los idiomas que lleguen)."""
    st = {"cuerpo": copy.deepcopy(cuerpo_viejo)}
    cliente = armar_cliente_precondiciones_ok(ClienteSimulado(), ident)
    cliente.responder("GET", ruta_choice(NOMBRE), lambda *_: (200, copy.deepcopy(st["cuerpo"]), {}))
    cliente.responder("GET", es_ruta_solutioncomponents, (200, {"value": [{"solutioncomponentid": "x"}]}, {}))

    def copiar(label):
        return {LOC: [{"Label": l["Label"], "LanguageCode": l["LanguageCode"]} for l in label[LOC]]}

    def al_put(ruta, cuerpo, solucion):
        st["cuerpo"]["DisplayName"] = copiar(cuerpo["DisplayName"])
        return (204, None, {})

    def al_actualizar_opcion(ruta, cuerpo, solucion):
        op = next(o for o in st["cuerpo"]["Options"] if o["Value"] == cuerpo["Value"])
        op["Label"] = copiar(cuerpo["Label"])
        return (204, None, {})

    cliente.responder("PUT", f"GlobalOptionSetDefinitions({META})", al_put)
    cliente.responder("POST", "UpdateOptionValue", al_actualizar_opcion)
    cliente.responder("POST", "PublishXml", (204, None, {}))
    return cliente


def viejo(displayname=None, etiquetas=None, descripciones=None, descripcion_choice=None, quitar_idioma=None, **otros):
    """El cuerpo del entorno con cambios. `displayname`: {lcid: texto}; `etiquetas`/`descripciones`: {(opcion, lcid): texto};
    `quitar_idioma`: {campo: lcid} para dejar la etiqueta de ese campo en un solo idioma."""
    c = cuerpo_choice_conforme(PRODUCTO)
    for lcid, texto in (displayname or {}).items():
        next(l for l in c["DisplayName"][LOC] if l["LanguageCode"] == lcid)["Label"] = texto
    for (i, lcid), texto in (etiquetas or {}).items():
        next(l for l in c["Options"][i]["Label"][LOC] if l["LanguageCode"] == lcid)["Label"] = texto
    for (i, lcid), texto in (descripciones or {}).items():
        next(l for l in c["Options"][i]["Description"][LOC] if l["LanguageCode"] == lcid)["Label"] = texto
    if descripcion_choice:
        for lcid, texto in descripcion_choice.items():
            next(l for l in c["Description"][LOC] if l["LanguageCode"] == lcid)["Label"] = texto
    for campo, lcid in (quitar_idioma or {}).items():
        etiqueta = c["DisplayName"] if campo == "DisplayName" else c["Options"][campo]["Label"]
        etiqueta[LOC] = [l for l in etiqueta[LOC] if l["LanguageCode"] != lcid]
    c.update(otros)
    return c


def con_valor_cambiado(cuerpo):
    cuerpo["Options"][1]["Value"] = 159460009
    return cuerpo


def con_opcion_menos(cuerpo):
    cuerpo["Options"] = cuerpo["Options"][:1]
    return cuerpo


def pares(label):
    return [(l["LanguageCode"], l["Label"]) for l in label[LOC]]


class CorregirNombres(unittest.TestCase):
    def correr(self, cliente, **kw):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "playbook.md")
            escribir_playbook(ruta, IDENTIDAD_VALIDA, PRODUCTO)
            return cg.construir(ruta, kw.pop("solo_verificar", False), lambda: cliente, **kw)

    def escrituras(self, cliente):
        return [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET"]

    def test_sin_el_flag_difiere_y_no_escribe(self):
        cliente = armar(viejo(displayname={3082: "CH - CARP - Producto crediticío"}))
        estado, _, detalle = self.correr(cliente)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("DisplayName[3082]", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_renombra_el_choice_y_las_opciones_que_difieren_y_publica(self):
        cliente = armar(viejo(displayname={3082: "CH - CARP - Producto crediticío"}, etiquetas={(1, 3082): "Tarjéta"}))
        estado, _, detalle = self.correr(cliente, corregir_nombres=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn("se corrigieron 2", detalle)
        self.assertEqual(self.escrituras(cliente), [("PUT", f"GlobalOptionSetDefinitions({META})"), ("POST", "UpdateOptionValue"), ("POST", "PublishXml")])
        put = next(l for l in cliente.llamadas if l["metodo"] == "PUT")
        self.assertEqual(put["cuerpo"]["@odata.type"], "Microsoft.Dynamics.CRM.OptionSetMetadata")
        self.assertEqual(put["cuerpo"]["Name"], NOMBRE)  # el resto de la definición se conserva
        self.assertEqual(put["cabeceras"], {"MSCRM.MergeLabels": "true"})
        self.assertEqual(put["solucion"], IDENTIDAD_VALIDA["solucion"])
        op = next(l for l in cliente.llamadas if l["ruta"] == "UpdateOptionValue")["cuerpo"]
        self.assertEqual((op["OptionSetName"], op["Value"], op["MergeLabels"], op["SolutionUniqueName"]), (NOMBRE, 159460002, True, IDENTIDAD_VALIDA["solucion"]))
        self.assertIn(NOMBRE, next(l for l in cliente.llamadas if l["ruta"] == "PublishXml")["cuerpo"]["ParameterXml"])

    def test_cada_escritura_manda_la_etiqueta_de_todos_los_idiomas(self):
        """Aunque solo difiera el 3082, el PUT y el UpdateOptionValue llevan los dos idiomas, cada uno con su texto."""
        cliente = armar(viejo(displayname={3082: "CH - CARP - Producto crediticío"}, etiquetas={(0, 3082): "Préstamo"}))
        self.correr(cliente, corregir_nombres=True)
        put = next(l for l in cliente.llamadas if l["metodo"] == "PUT")
        self.assertEqual(pares(put["cuerpo"]["DisplayName"]), [(1033, "CH - CARP - Credit product"), (3082, "CH - CARP - Producto crediticio")])
        op = next(l for l in cliente.llamadas if l["ruta"] == "UpdateOptionValue")["cuerpo"]
        self.assertEqual(op["Value"], 159460001)
        self.assertEqual(pares(op["Label"]), [(1033, "Loan"), (3082, "Prestamo")])

    def test_con_un_solo_idioma_o_con_tres_corrige_exactamente_los_declarados(self):
        casos = [
            ("un idioma", [1033], {"1033": "CH - CARP - Credit product"}, {"1033": "Loan"}),
            ("tres idiomas", [1033, 3082, 2058], {"1033": "CH - CARP - Credit product", "3082": "CH - CARP - Producto crediticio", "2058": "CH - CARP - Producto"},
             {"1033": "Loan", "3082": "Prestamo", "2058": "Prestamo mx"}),
        ]
        for nombre, idiomas, display, etiqueta in casos:
            with self.subTest(nombre):
                ident = identidad(idiomas=idiomas)
                comp = componente(nombre=NOMBRE, displayname=display, descripcion="Producto del arreglo.",
                                  opciones=[{"valor": 159460001, "etiqueta": etiqueta, "descripcion": "Préstamo"}])
                viejo_3 = cuerpo_choice_conforme(comp, idiomas)
                next(l for l in viejo_3["DisplayName"][LOC] if l["LanguageCode"] == idiomas[-1])["Label"] = "CH - CARP - Créditó"
                next(l for l in viejo_3["Options"][0]["Label"][LOC] if l["LanguageCode"] == idiomas[0])["Label"] = "Lóan"
                cliente = armar(viejo_3, ident)
                with tempfile.TemporaryDirectory() as d:
                    ruta = os.path.join(d, "playbook.md")
                    escribir_playbook(ruta, ident, comp)
                    estado, _, detalle = cg.construir(ruta, False, lambda: cliente, corregir_nombres=True)
                self.assertEqual(estado, "ya_existia", detalle)
                put = next(l for l in cliente.llamadas if l["metodo"] == "PUT")
                self.assertEqual([c for c, _ in pares(put["cuerpo"]["DisplayName"])], idiomas)
                op = next(l for l in cliente.llamadas if l["ruta"] == "UpdateOptionValue")["cuerpo"]
                self.assertEqual(pares(op["Label"]), [(int(k), v) for k, v in etiqueta.items()])

    def test_cada_diferencia_de_nombre_visible_se_reconoce_y_se_corrige(self):
        put = ("PUT", f"GlobalOptionSetDefinitions({META})")
        opcion = ("POST", "UpdateOptionValue")
        publicar = ("POST", "PublishXml")
        casos = [
            ("displayname en 1033", viejo(displayname={1033: "CH - CARP - Créditó"}), [put, publicar]),
            ("displayname en 3082", viejo(displayname={3082: "CH - CARP - Créditó"}), [put, publicar]),
            ("displayname en un solo idioma (falta el 3082)", viejo(quitar_idioma={"DisplayName": 3082}), [put, publicar]),
            ("etiqueta de opción en 1033", viejo(etiquetas={(0, 1033): "Préstamo"}), [opcion, publicar]),
            ("etiqueta de opción en 3082", viejo(etiquetas={(1, 3082): "Tarjéta"}), [opcion, publicar]),
            ("etiqueta de opción en un solo idioma (falta el 1033)", viejo(quitar_idioma={1: 1033}), [opcion, publicar]),
            ("dos opciones", viejo(etiquetas={(0, 1033): "Préstamo", (1, 3082): "Tarjéta"}), [opcion, opcion, publicar]),
        ]
        for nombre, cuerpo, esperadas in casos:
            with self.subTest(nombre):
                cliente = armar(cuerpo)
                estado, _, detalle = self.correr(cliente, corregir_nombres=True)
                self.assertEqual(estado, "ya_existia", detalle)
                self.assertIn("solo diferían nombres visibles", detalle)
                self.assertEqual(self.escrituras(cliente), esperadas)

    def test_una_opcion_que_difiere_en_los_dos_idiomas_se_corrige_una_sola_vez(self):
        cliente = armar(viejo(etiquetas={(0, 1033): "Préstamo", (0, 3082): "Préstamo"}))
        estado, _, detalle = self.correr(cliente, corregir_nombres=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn("se corrigieron 1 ", detalle)
        self.assertEqual(self.escrituras(cliente), [("POST", "UpdateOptionValue"), ("POST", "PublishXml")])

    def test_solo_las_opciones_no_hace_put(self):
        cliente = armar(viejo(etiquetas={(0, 1033): "Lóan"}))
        estado, _, detalle = self.correr(cliente, corregir_nombres=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual([m for m, _ in self.escrituras(cliente) if m == "PUT"], [])

    def test_con_otra_diferencia_ademas_no_toca_nada(self):
        casos = [
            ("descripción del choice en 3082", viejo(displayname={3082: "CH - CARP - Créditó"}, descripcion_choice={3082: "Otra descripción"})),
            ("descripción de una opción en 1033", viejo(displayname={3082: "CH - CARP - Créditó"}, descripciones={(0, 1033): "Otra"})),
            ("valor de una opción", con_valor_cambiado(viejo(etiquetas={(0, 1033): "Lóan"}))),
            ("choice managed", viejo(displayname={1033: "CH - CARP - Créditó"}, IsManaged=True)),
            ("cantidad de opciones", con_opcion_menos(viejo(displayname={1033: "CH - CARP - Créditó"}))),
        ]
        for nombre, cuerpo in casos:
            with self.subTest(nombre):
                cliente = armar(cuerpo)
                estado, _, detalle = self.correr(cliente, corregir_nombres=True)
                self.assertEqual(estado, "difiere", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_una_etiqueta_en_un_idioma_no_declarado_no_se_corrige_y_difiere(self):
        """Con MergeLabels no se puede quitar una etiqueta de más: corregir nombres no la arregla, así que no toca nada."""
        con_extra_choice = viejo(displayname={3082: "CH - CARP - Créditó"})
        con_extra_choice["DisplayName"][LOC].append({"Label": "x", "LanguageCode": 2058})
        con_extra_opcion = viejo(etiquetas={(0, 1033): "Lóan"})
        con_extra_opcion["Options"][1]["Label"][LOC].append({"Label": "x", "LanguageCode": 2058})
        for nombre, cuerpo in (("en el displayname", con_extra_choice), ("en una opción", con_extra_opcion)):
            with self.subTest(nombre):
                cliente = armar(cuerpo)
                estado, _, detalle = self.correr(cliente, corregir_nombres=True)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn("no declara", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_con_solo_verificar_no_escribe(self):
        cliente = armar(viejo(displayname={3082: "CH - CARP - Créditó"}))
        estado, _, detalle = self.correr(cliente, corregir_nombres=True, solo_verificar=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_si_coincide_no_escribe(self):
        cliente = armar(viejo())
        estado, _, detalle = self.correr(cliente, corregir_nombres=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertNotIn("se corrigieron", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_si_una_escritura_falla_es_error_y_dice_cual(self):
        casos = [
            ("el PUT del nombre", viejo(displayname={3082: "CH - CARP - Créditó"}), ("PUT", f"GlobalOptionSetDefinitions({META})"), "falló cambiar el nombre visible del choice"),
            ("la etiqueta de una opción", viejo(etiquetas={(0, 1033): "Lóan"}), ("POST", "UpdateOptionValue"), "opciones[0]"),
            ("la publicación", viejo(etiquetas={(0, 1033): "Lóan"}), ("POST", "PublishXml"), "falló publicar"),
        ]
        for nombre, cuerpo, (metodo, ruta), fragmento in casos:
            with self.subTest(nombre):
                cliente = ClienteSimulado()
                cliente.responder(metodo, ruta, (400, {"error": "boom"}, {}))  # la primera regla que coincide gana
                cliente._reglas += armar(cuerpo)._reglas
                estado, _, detalle = self.correr(cliente, corregir_nombres=True)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("HTTP 400", detalle)
                self.assertIn(fragmento, detalle)

    def test_si_el_put_no_cambia_nada_la_verificacion_final_lo_dice(self):
        """Un 204 no verifica: si tras la corrección el entorno sigue difiriendo, el estado es `difiere`, no `ya_existia`."""
        cliente = armar(viejo(displayname={3082: "CH - CARP - Créditó"}))
        cliente._reglas = [r for r in cliente._reglas if r[0] != "PUT"]
        cliente.responder("PUT", f"GlobalOptionSetDefinitions({META})", (204, None, {}))
        cliente.responder("POST", "PublishXml", (204, None, {}))
        estado, _, detalle = self.correr(cliente, corregir_nombres=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("DisplayName[3082]", detalle)

    def test_el_get_completo_del_choice_que_falla_es_error(self):
        cliente = armar(viejo(displayname={3082: "CH - CARP - Créditó"}))
        respuestas = iter([200, 500])
        original = next(r for r in cliente._reglas if r[0] == "GET" and r[1] == ruta_choice(NOMBRE))
        cliente._reglas.remove(original)

        def leer(ruta, cuerpo, sol):
            if next(respuestas, 500) == 200:
                return original[2](ruta, cuerpo, sol)
            return (500, {"error": "boom"}, {})

        # dos lecturas: la de _verificar (200) y la lectura completa antes del PUT (500)
        cliente._reglas.insert(0, ("GET", ruta_choice(NOMBRE), leer))
        estado, _, detalle = self.correr(cliente, corregir_nombres=True)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("la lectura completa del choice", detalle)
        self.assertIn("HTTP 500", detalle)
        self.assertFalse(cliente.hubo_escritura())


if __name__ == "__main__":
    unittest.main()
