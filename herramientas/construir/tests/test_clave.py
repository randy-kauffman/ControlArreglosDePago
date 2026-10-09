"""Herramienta `clave.py`: una clave alternativa, desde un playbook. Ninguna
prueba toca la red ni espera de verdad.

El nombre visible (`displayname`) se escribe una vez por CADA idioma de
`idiomas` y se verifica en cada idioma, en los dos sentidos. Los datos salen
del diccionario de datos de CARP, sección 7 (K-01, K-02 y K-03)."""
import copy
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import clave as cv  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import (  # noqa: E402
    IDENTIDAD_VALIDA,
    armar_cliente_precondiciones_ok,
    es_ruta_solutioncomponents,
    es_ruta_solutions,
    identidad,
    label,
    playbook_md,
    por_idioma,
    respuesta_idioma_ok,
    respuesta_solucion_ok,
)

BORRAR = object()
ID_TABLA = "99999999-9999-9999-9999-999999999999"
IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="clave", inventario="5.4")
META = "77777777-7777-7777-7777-777777777777"

# K-02 del diccionario: clave del número de arreglo. Texto distinto en cada idioma a propósito, para que una herramienta
# que mandara el mismo texto a los dos idiomas (o el de uno solo) no pase por casualidad.
TABLA = "sanic_carp_tbl_arreglo"
CLAVE = "sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo"
BASE = {"tipo": "clave", "nombre": CLAVE,
        "displayname": {"1033": "KEY - CARP - Arreglo - Product, product number and arrangement number",
                        "3082": "KEY - CARP - Arreglo - Producto, numero de producto y numero de arreglo"},
        "tabla": TABLA, "columnas": ["sanic_producto", "sanic_numeroproducto", "sanic_numeroarreglo"]}
# K-01: una sola columna de texto, en la tabla de conversaciones. K-03: la primaria de la versión de plantilla.
K01 = {"tipo": "clave", "nombre": "sanic_carp_key_conversacion_huellamessageid", "displayname": por_idioma("KEY - CARP - Conversacion - Huella del Message-ID"),
       "tabla": "sanic_carp_tbl_conversacion", "columnas": ["sanic_huellamessageid"]}
K03 = {"tipo": "clave", "nombre": "sanic_carp_key_versionplantilla_nombre", "displayname": por_idioma("KEY - CARP - Version de plantilla - Numero de version"),
       "tabla": "sanic_carp_tbl_versionplantilla", "columnas": ["sanic_nombre"]}
# La cédula del cliente lleva seguridad de columna (diccionario, sección 7): no puede ser clave.
K_CEDULA = {"tipo": "clave", "nombre": "sanic_carp_key_cliente_cedula", "displayname": por_idioma("KEY - CARP - Cliente - Cedula"),
            "tabla": "sanic_carp_tbl_cliente", "columnas": ["sanic_cedula"]}


def con_cambio(obj, ruta, valor):
    n = copy.deepcopy(obj)
    nodo = n
    for p in ruta[:-1]:
        nodo = nodo[p]
    if valor is BORRAR:
        del nodo[ruta[-1]]
    else:
        nodo[ruta[-1]] = valor
    return n


def col(nombre, tipo, **kw):
    return dict({"LogicalName": nombre, "AttributeTypeName": {"Value": tipo}, "IsSecured": False, "AttributeOf": None}, **kw)


COLUMNAS = [col("sanic_producto", "PicklistType"), col("sanic_numeroproducto", "StringType"), col("sanic_numeroarreglo", "IntegerType"),
            col("sanic_clienteid", "LookupType"), col("sanic_comentarios", "MemoType"), col("sanic_cuentadebitar", "StringType")]
LARGOS = [{"LogicalName": "sanic_numeroproducto", "MaxLength": 30}, {"LogicalName": "sanic_cedula", "MaxLength": 16},
          {"LogicalName": "sanic_cuentadebitar", "MaxLength": 451}, {"LogicalName": "sanic_huellamessageid", "MaxLength": 64},
          {"LogicalName": "sanic_nombre", "MaxLength": 10}]
COLUMNAS_OTRAS_TABLAS = [col("sanic_huellamessageid", "StringType"), col("sanic_nombre", "StringType"), col("sanic_cedula", "StringType", IsSecured=True)]


def cuerpo_clave(d, estado="Active", idiomas=None):
    return {"MetadataId": META, "LogicalName": d["nombre"], "SchemaName": d["nombre"], "KeyAttributes": list(d["columnas"]),
            "EntityKeyIndexStatus": estado, "IsManaged": False, "DisplayName": label(d["displayname"], idiomas), "AsyncJob": None}


def armar(cliente, d, existe=True, clave=None, estados=None, componentes=None, columnas=None, largos=None, base=1033, ident=None):
    """Configura TODAS las rutas del camino feliz. `estados`: secuencia de EntityKeyIndexStatus que devuelve el entorno en
    lecturas sucesivas. `base`: idioma base del entorno."""
    ident = IDENT if ident is None else ident
    cliente.responder("GET", es_ruta_solutions, respuesta_solucion_ok(ident))
    cliente.responder("GET", "organizations?$select=languagecode", respuesta_idioma_ok(ident, base=base))
    cliente.responder("GET", "RetrieveProvisionedLanguages", (200, {"RetrieveProvisionedLanguages": list(ident["idiomas"])}, {}))
    st = {"existe": existe, "estados": list(estados or ["Active"])}
    cliente.responder("GET", lambda r: r.startswith(f"EntityDefinitions(LogicalName='{d['tabla']}')?"), (200, {"LogicalName": d["tabla"], "MetadataId": ID_TABLA}, {}))
    cliente.responder("GET", lambda r: "/Attributes/Microsoft.Dynamics.CRM.StringAttributeMetadata" in r, (200, {"value": largos if largos is not None else LARGOS}, {}))
    cliente.responder("GET", lambda r: "/Attributes?" in r, (200, {"value": columnas if columnas is not None else COLUMNAS + COLUMNAS_OTRAS_TABLAS[:2]}, {}))

    def leer(*_):
        if not st["existe"]:
            return (404, {"error": "no existe"}, {})
        if clave is not None:
            return (200, clave, {})
        e = st["estados"].pop(0) if len(st["estados"]) > 1 else st["estados"][0]
        return (200, cuerpo_clave(d, e, ident["idiomas"]), {})

    cliente.responder("GET", lambda r: "/Keys(LogicalName=" in r, leer)
    cliente.responder("GET", es_ruta_solutioncomponents, (200, {"value": componentes if componentes is not None else [{"rootcomponentbehavior": 0}]}, {}))

    def al_crear(*_):
        st["existe"] = True
        return (204, None, {})

    cliente.responder("POST", f"EntityDefinitions(LogicalName='{d['tabla']}')/Keys", al_crear)
    return cliente


class Base(unittest.TestCase):
    def setUp(self):
        self.esperas = []

    def correr(self, fabrica, datos, solo_verificar=False, ident=None):
        with tempfile.TemporaryDirectory() as dd:
            ruta = os.path.join(dd, "playbook.md")
            with open(ruta, "w", encoding="utf-8") as f:
                f.write(playbook_md(IDENT if ident is None else ident, datos))
            return cv.construir(ruta, solo_verificar, fabrica, dormir=self.esperas.append)

    def con_cliente(self, cliente, datos, **kw):
        return self.correr(lambda: cliente, datos, **kw)


class ValidacionSinRed(Base):
    CASOS = [
        (["tipo"], "tabla", "tipo"),
        (["nombre"], "sanic_carp_key_Arreglo", "nombre"),
        (["nombre"], "sanic_carp_key_conversacion_huella", "sanic_carp_key_arreglo_"),
        (["nombre"], "sanic_carp_key_arreglo_" + "a" * 90, "95"),
        (["nombre"], "sanic_otra_key_arreglo_producto", "sanic_carp_key_arreglo_"),
        (["tabla"], "arreglo", "tabla"),
        (["tabla"], "sanic_otra_tbl_arreglo", "tabla"),
        (["columnas"], [], "columnas"),
        (["columnas"], "sanic_producto", "columnas"),
        (["columnas"], ["sanic_producto", "sanic_producto"], "repetida"),
        (["columnas"], ["sanic_Producto"], "columnas[0]"),
        (["columnas"], ["sanic_c%d" % i for i in range(17)], "16"),
        (["columnas", 0], 5, "columnas[0]"),
        (["sobra"], 1, "sobra"),
        (["columnas"], BORRAR, "faltan"),
    ]

    def test_cada_dato_invalido_es_error_nombra_la_clave_y_no_toca_la_red(self):
        for ruta, valor, esperado in self.CASOS:
            with self.subTest(f"{ruta} <- {'BORRAR' if valor is BORRAR else repr(valor)[:40]}"):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, con_cambio(BASE, ruta, valor))
                self.assertEqual(estado, "error", detalle)
                self.assertIn(esperado, detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(centinela.llamada)


class NombreVisiblePorIdioma(Base):
    """El `displayname` es un objeto con un texto por CADA idioma de `idiomas`: ni cadena única, ni idiomas de menos o de más."""

    CASOS = [
        ("cadena única (formato viejo)", ["displayname"], "KEY - CARP - Arreglo - Producto", ("'displayname'", "objeto")),
        ("ni objeto ni texto", ["displayname"], None, ("'displayname'", "objeto")),
        ("falta el 3082", ["displayname"], {"1033": "KEY - CARP - Arreglo"}, ("'displayname'", "faltan ['3082']")),
        ("falta el 1033", ["displayname"], {"3082": "KEY - CARP - Arreglo"}, ("'displayname'", "faltan ['1033']")),
        ("sobra el 2058", ["displayname"], {"1033": "KEY - CARP - Arreglo", "3082": "KEY - CARP - Arreglo", "2058": "KEY - CARP - Arreglo"}, ("'displayname'", "sobran ['2058']")),
        ("texto vacío en 3082", ["displayname", "3082"], "", ("'displayname'[3082]", "texto no vacío")),
        ("texto que no es cadena en 1033", ["displayname", "1033"], 3, ("'displayname'[1033]", "texto no vacío")),
        ("sin el patrón KEY en 1033", ["displayname", "1033"], "Clave del arreglo", ("'displayname'[1033]", "KEY - CARP - ")),
        ("otra abreviatura solo en 3082", ["displayname", "3082"], "KEY - OTRA - Arreglo", ("'displayname'[3082]", "KEY - CARP - ")),
        ("minúscula en 3082", ["displayname", "3082"], "key - carp - Arreglo", ("'displayname'[3082]", "KEY - CARP - ")),
    ]

    def test_cada_caso_es_error_nombra_el_campo_y_el_idioma_y_no_toca_la_red(self):
        for descripcion, ruta, valor, fragmentos in self.CASOS:
            with self.subTest(descripcion):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, con_cambio(BASE, ruta, valor))
                self.assertEqual(estado, "error", detalle)
                self.assertFalse(centinela.llamada)
                self.assertNotIn("fallo inesperado", detalle)
                for f in fragmentos:
                    self.assertIn(f, detalle)

    def test_los_idiomas_de_identidad_y_los_del_displayname_tienen_que_coincidir(self):
        for ident, fragmento in ((identidad(idiomas=[1033]), "sobran ['3082']"), (identidad(idiomas=[1033, 3082, 2058]), "faltan ['2058']")):
            with self.subTest(fragmento):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, BASE, ident=dict(ident, tipo_playbook="clave"))
                self.assertEqual(estado, "error", detalle)
                self.assertIn(fragmento, detalle)
                self.assertFalse(centinela.llamada)

    def test_la_identidad_vieja_con_lcid_es_error_y_no_toca_la_red(self):
        vieja = {k: v for k, v in IDENT.items() if k != "idiomas"}
        vieja["lcid"] = 1033
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, BASE, ident=vieja)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("faltan ['idiomas']", detalle)
        self.assertIn("sobran ['lcid']", detalle)
        self.assertFalse(centinela.llamada)

    def test_tilde_o_enie_en_cada_idioma_se_rechaza_sin_excepciones(self):
        """BP-PP-197 en cada idioma. No hay excepciones heredadas: la palabra del diccionario con su tilde también se rechaza."""
        casos = [("1033", "KEY - CARP - Conversación - Huella del Message-ID"), ("3082", "KEY - CARP - Versión de plantilla - Número de versión"),
                 ("3082", "KEY - CARP - Arreglo - Año")]
        for idioma, texto in casos:
            with self.subTest(f"{idioma} {texto}"):
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, con_cambio(K01, ["displayname", idioma], texto))
                self.assertEqual(estado, "error", detalle)
                self.assertIn("BP-PP-197", detalle)
                self.assertIn(f"'displayname'[{idioma}]", detalle)
                self.assertFalse(centinela.llamada)

    def test_un_idioma_basta_si_identidad_y_displayname_coinciden(self):
        ident = dict(identidad(idiomas=[1033]), tipo_playbook="clave")
        datos = con_cambio(BASE, ["displayname"], {"1033": "KEY - CARP - Arreglo - Number"})
        cliente = armar(ClienteSimulado(), datos, existe=False, ident=ident)
        estado, _, detalle = self.con_cliente(cliente, datos, ident=ident)
        self.assertEqual(estado, "creado", detalle)


class Payload(unittest.TestCase):
    def test_cuerpo(self):
        p = cv.construir_payload(BASE, IDENT)
        self.assertEqual(p["@odata.type"], "Microsoft.Dynamics.CRM.EntityKeyMetadata")
        self.assertEqual(p["SchemaName"], CLAVE)
        self.assertEqual(p["KeyAttributes"], ["sanic_producto", "sanic_numeroproducto", "sanic_numeroarreglo"])
        self.assertEqual(p["DisplayName"]["@odata.type"], "Microsoft.Dynamics.CRM.Label")

    def test_el_nombre_visible_viaja_una_vez_por_idioma_con_su_texto(self):
        p = cv.construir_payload(BASE, IDENT)
        self.assertEqual(p["DisplayName"]["LocalizedLabels"], [
            {"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": BASE["displayname"]["1033"], "LanguageCode": 1033},
            {"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": BASE["displayname"]["3082"], "LanguageCode": 3082},
        ])

    def test_con_tres_idiomas_viajan_los_tres_en_el_orden_de_identidad(self):
        ident = identidad(idiomas=[1033, 3082, 2058])
        datos = con_cambio(BASE, ["displayname"], por_idioma("KEY - CARP - Arreglo - Numero", [1033, 3082, 2058]))
        p = cv.construir_payload(datos, ident)
        self.assertEqual([l["LanguageCode"] for l in p["DisplayName"]["LocalizedLabels"]], [1033, 3082, 2058])


class Precondiciones(Base):
    def bloquea(self, datos, esperado, **kw):
        cliente = armar(ClienteSimulado(), datos, existe=False, **kw)
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn(esperado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_columna_no_existe(self):
        self.bloquea(con_cambio(BASE, ["columnas"], ["sanic_producto", "sanic_inexistente"]), "sanic_inexistente")

    def test_la_cedula_lleva_seguridad_de_columna_y_no_puede_ser_clave(self):
        self.bloquea(K_CEDULA, "seguridad de columna", columnas=COLUMNAS_OTRAS_TABLAS)

    def test_un_tipo_que_no_admite_clave(self):
        self.bloquea(con_cambio(BASE, ["columnas"], ["sanic_comentarios"]), "MemoType")

    def test_supera_los_900_bytes(self):
        self.bloquea(con_cambio(BASE, ["columnas"], ["sanic_cuentadebitar"]), "902")

    def test_450_caracteres_es_justo_el_limite(self):
        largos = [{"LogicalName": "sanic_cuentadebitar", "MaxLength": 450}]
        d = con_cambio(BASE, ["columnas"], ["sanic_cuentadebitar"])
        cliente = armar(ClienteSimulado(), d, existe=False, largos=largos)
        estado, _, detalle = self.con_cliente(cliente, d)
        self.assertEqual(estado, "creado", detalle)

    def test_las_tres_claves_del_diccionario_entran_en_el_limite(self):
        """K-01 (64 caracteres), K-02 (choice + texto de 30 + entero) y K-03 (texto de 10) se crean."""
        for datos in (K01, BASE, K03):
            with self.subTest(datos["nombre"]):
                cliente = armar(ClienteSimulado(), datos, existe=False, columnas=COLUMNAS + COLUMNAS_OTRAS_TABLAS[:2])
                estado, _, detalle = self.con_cliente(cliente, datos)
                self.assertEqual(estado, "creado", detalle)


class IdiomasDelEntorno(Base):
    def test_el_idioma_base_del_entorno_puede_ser_el_3082_si_el_playbook_declara_los_dos(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False, base=3082)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "creado", detalle)
        cliente = armar(ClienteSimulado(), BASE, base=3082)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_el_idioma_base_que_el_playbook_no_declara_es_bloqueado(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False, base=2058)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("idioma base del entorno (2058)", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_idioma_del_playbook_no_provisionado_es_bloqueado(self):
        for provisionados in ([1033], [3082], [2058]):
            with self.subTest(provisionados):
                cliente = ClienteSimulado()
                cliente.responder("GET", "RetrieveProvisionedLanguages", (200, {"RetrieveProvisionedLanguages": provisionados}, {}))
                armar(cliente, BASE, existe=False, base=provisionados[0])
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "bloqueado", detalle)
                self.assertIn("no están provisionados", detalle)
                self.assertIn(str([i for i in IDENT["idiomas"] if i not in provisionados]), detalle)
                self.assertFalse(cliente.hubo_escritura())


class Caminos(Base):
    def test_se_crea_y_espera_a_que_el_indice_este_activo(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False, estados=["Pending", "InProgress", "Active", "Active"])
        estado, comp, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(comp, CLAVE)
        self.assertIn(META, detalle)
        self.assertEqual(self.esperas, [cv.ESPERA_INDICE_SEGUNDOS] * 2)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual([p["ruta"] for p in posts], [f"EntityDefinitions(LogicalName='{TABLA}')/Keys"])
        self.assertEqual(posts[0]["solucion"], IDENT["solucion"])
        self.assertEqual(posts[0]["cuerpo"], cv.construir_payload(BASE, IDENT))

    def test_si_el_indice_falla_lo_dice_y_nombra_la_reactivacion(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False, estados=["Pending", "Failed", "Failed"])
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("Failed", detalle)
        self.assertIn("ReactivateEntityKey", detalle)

    def test_si_el_indice_no_termina_a_tiempo_lo_dice(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False, estados=["Pending"])
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("Pending", detalle)
        self.assertEqual(len(self.esperas), cv.INTENTOS_INDICE)

    def test_ya_existia_no_escribe_ni_espera(self):
        cliente = armar(ClienteSimulado(), BASE)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn(META, detalle)
        self.assertFalse(cliente.hubo_escritura())
        self.assertEqual(self.esperas, [])

    def test_existe_pero_el_indice_no_esta_activo(self):
        for e, esperado in (("Failed", "difiere"), ("InProgress", "error")):
            with self.subTest(e):
                cliente = armar(ClienteSimulado(), BASE, estados=[e])
                estado, _, detalle = self.con_cliente(cliente, BASE, solo_verificar=True)
                self.assertEqual(estado, esperado, detalle)
                self.assertIn(e, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_sin_clave_es_error_y_no_crea(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE, solo_verificar=True)
        self.assertEqual(estado, "error")
        self.assertIn("no existe", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_creacion_falla(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", f"EntityDefinitions(LogicalName='{TABLA}')/Keys", (400, {"error": "clave duplicada"}, {}))
        armar(cliente, BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error")
        self.assertIn("HTTP 400", detalle)
        self.assertIn("la creación falló", detalle)

    def test_204_pero_no_aparece_al_releer_es_error(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False)
        cliente._reglas = [r for r in cliente._reglas if r[0] != "POST"]
        cliente.responder("POST", f"EntityDefinitions(LogicalName='{TABLA}')/Keys", (204, None, {}))
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error")
        self.assertIn("no aparece al releer", detalle)

    def test_204_con_relectura_que_trae_un_idioma_menos_es_error(self):
        """El 204 no verifica nada: si al releer falta la etiqueta de un idioma, no se informa creado."""
        un_solo_idioma = cuerpo_clave(BASE)
        un_solo_idioma["DisplayName"]["LocalizedLabels"] = un_solo_idioma["DisplayName"]["LocalizedLabels"][:1]
        respuestas = iter([(404, {}, {}), (200, un_solo_idioma, {})])
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: "/Keys(LogicalName=" in r, lambda *_: next(respuestas))  # la primera regla que coincide gana
        armar(cliente, BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error")
        self.assertIn("no coincide", detalle)
        self.assertIn("DisplayName[3082]", detalle)


class Pertenencia(Base):
    def test_se_comprueba_la_de_la_tabla_porque_una_clave_no_es_un_componente_propio(self):
        """Visto el 2026-09-21 en el ensayo: una clave recién creada no tiene fila en
        solutioncomponents (ni tipo 14 ni ningún otro); viaja dentro de su tabla."""
        cliente = armar(ClienteSimulado(), BASE)
        self.con_cliente(cliente, BASE)
        rutas = [l["ruta"] for l in cliente.llamadas if es_ruta_solutioncomponents(l["ruta"])]
        self.assertEqual(len(rutas), 1)
        self.assertIn(f"objectid eq {ID_TABLA}", rutas[0])
        self.assertIn("componenttype eq 1", rutas[0])


class Diferencias(Base):
    def test_el_orden_de_las_columnas_no_es_una_diferencia(self):
        """La plataforma devuelve KeyAttributes en su propio orden (alfabético)."""
        clave = con_cambio(cuerpo_clave(BASE), ["KeyAttributes"], ["sanic_numeroarreglo", "sanic_numeroproducto", "sanic_producto"])
        estado, _, detalle = self.con_cliente(armar(ClienteSimulado(), BASE, clave=clave), BASE)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_el_orden_de_las_etiquetas_no_es_una_diferencia(self):
        clave = cuerpo_clave(BASE)
        clave["DisplayName"]["LocalizedLabels"].reverse()
        estado, _, detalle = self.con_cliente(armar(ClienteSimulado(), BASE, clave=clave), BASE)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_cada_diferencia(self):
        c = cuerpo_clave(BASE)
        casos = [("KeyAttributes", dict(clave=con_cambio(c, ["KeyAttributes"], ["sanic_producto"]))),
                 ("IsManaged", dict(clave=con_cambio(c, ["IsManaged"], True))),
                 ("pertenencia a la solución", dict(componentes=[])),
                 ("no incluye todos sus subcomponentes", dict(componentes=[{"rootcomponentbehavior": 1}]))]
        for esperado, kw in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), BASE, **kw)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_el_nombre_visible_se_verifica_en_cada_idioma_en_los_dos_sentidos(self):
        c = cuerpo_clave(BASE)

        def con_label(mutar):
            n = copy.deepcopy(c)
            mutar(n["DisplayName"]["LocalizedLabels"])
            return n

        def cambiar(locs, lcid, texto):
            next(l for l in locs if l["LanguageCode"] == lcid)["Label"] = texto

        casos = [
            ("texto distinto en 1033", con_label(lambda ls: cambiar(ls, 1033, "Otro")),
             f"DisplayName[1033]: entorno='Otro' playbook={BASE['displayname']['1033']!r}"),
            ("texto distinto en 3082", con_label(lambda ls: cambiar(ls, 3082, "Otro")),
             f"DisplayName[3082]: entorno='Otro' playbook={BASE['displayname']['3082']!r}"),
            ("el entorno trae solo el 1033", con_label(lambda ls: ls.pop(1)),
             f"DisplayName[3082]: entorno=None playbook={BASE['displayname']['3082']!r}"),
            ("el entorno trae solo el 3082", con_label(lambda ls: ls.pop(0)),
             f"DisplayName[1033]: entorno=None playbook={BASE['displayname']['1033']!r}"),
            ("el entorno trae una etiqueta de más en el 2058", con_label(lambda ls: ls.append({"Label": "x", "LanguageCode": 2058})),
             "DisplayName tiene etiquetas en un idioma que el playbook no declara: [2058]"),
        ]
        for nombre, clave, esperado in casos:
            with self.subTest(nombre):
                cliente = armar(ClienteSimulado(), BASE, clave=clave)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "difiere", detalle)
                self.assertEqual(detalle, esperado)
                self.assertFalse(cliente.hubo_escritura())


class FormaYHttpInesperados(Base):
    def test_http_inesperado_es_error_y_nombra_la_consulta(self):
        for matcher, nombre in [(lambda r: r.startswith("EntityDefinitions(LogicalName=") and "/" not in r.split(")", 1)[1][:2], "EntityDefinitions"),
                                (lambda r: "/Attributes?" in r, "Attributes"), (lambda r: "StringAttributeMetadata" in r, "StringAttributeMetadata"),
                                (lambda r: "/Keys(LogicalName=" in r, "Keys"), (es_ruta_solutioncomponents, "solutioncomponents"), (es_ruta_solutions, "solutions")]:
            with self.subTest(nombre):
                cliente = ClienteSimulado()
                cliente.responder("GET", matcher, (500, {"error": "boom"}, {}))
                armar(cliente, BASE)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("HTTP 500", detalle)
                self.assertIn(nombre, detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_forma_inesperada_es_error_y_nombra_el_campo(self):
        c = cuerpo_clave(BASE)
        casos = [("MetadataId", dict(clave=con_cambio(c, ["MetadataId"], None))), ("KeyAttributes", dict(clave=con_cambio(c, ["KeyAttributes"], "sanic_producto"))),
                 ("KeyAttributes[0]", dict(clave=con_cambio(c, ["KeyAttributes"], [None, "sanic_producto"]))),
                 ("EntityKeyIndexStatus", dict(clave=con_cambio(c, ["EntityKeyIndexStatus"], 2))), ("IsManaged", dict(clave=con_cambio(c, ["IsManaged"], "false"))),
                 ("DisplayName", dict(clave=con_cambio(c, ["DisplayName"], None))),
                 ("DisplayName.LocalizedLabels[0].LanguageCode", dict(clave=con_cambio(c, ["DisplayName", "LocalizedLabels", 0, "LanguageCode"], "1033"))),
                 ("value[0].rootcomponentbehavior", dict(componentes=[{"rootcomponentbehavior": "0"}])),
                 ("value[1].IsSecured", dict(columnas=con_cambio(COLUMNAS, [1, "IsSecured"], None))),
                 ("value[0].AttributeTypeName.Value", dict(columnas=con_cambio(COLUMNAS, [0, "AttributeTypeName", "Value"], 5)))]
        for campo, kw in casos:
            with self.subTest(campo):
                cliente = armar(ClienteSimulado(), BASE, **kw)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())


class Uso(unittest.TestCase):
    def test_sin_ruta_es_error_de_uso(self):
        import unittest.mock as mock

        with mock.patch.object(sys, "argv", ["clave.py"]), mock.patch("builtins.print") as imprimir:
            codigo = cv.main()
        self.assertEqual(codigo, 1)
        self.assertIn("uso incorrecto", imprimir.call_args[0][0])


if __name__ == "__main__":
    unittest.main()
