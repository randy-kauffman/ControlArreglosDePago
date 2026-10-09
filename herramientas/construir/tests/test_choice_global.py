"""Pruebas de `choice_global.py`. Nada acá toca el entorno real: la parte
offline se prueba con `FabricaCentinela` (demuestra que ni se intenta
construir un cliente), y la parte contra "el entorno" con `ClienteSimulado`
(demuestra qué se pidió y, sobre todo, que nunca se llegó a escribir nada
salvo en el único camino feliz de creación).

Todo nombre visible (displayname del choice y etiqueta de cada opción) se
escribe una vez por CADA idioma de `idiomas` (skill 0.2.0) y se verifica en
cada idioma, en los dos sentidos. La descripción es prosa: el mismo texto en
cada idioma. Los datos de ejemplo salen del diccionario de datos de CARP
(sección 3.1)."""
import copy
import os
import sys
import tempfile
import unittest
import unittest.mock as mock

_AQUI = os.path.dirname(os.path.abspath(__file__))
_CONSTRUIR = os.path.dirname(_AQUI)
if _CONSTRUIR not in sys.path:
    sys.path.insert(0, _CONSTRUIR)

import choice_global as cg  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaFalla, FabricaCentinela  # noqa: E402
from fixtures import (  # noqa: E402
    COMPONENTE_VALIDO,
    IDENTIDAD_VALIDA,
    armar_cliente_precondiciones_ok,
    componente,
    cuerpo_choice_conforme,
    es_ruta_solutioncomponents,
    es_ruta_solutions,
    escribir_playbook,
    identidad,
    por_idioma,
    respuesta_idioma_ok,
    respuesta_idiomas_provisionados_ok,
    respuesta_solucion_ok,
    ruta_choice,
)

BORRAR = object()
RUTA_ORG = "organizations?$select=languagecode"
RUTA_IDIOMAS = "RetrieveProvisionedLanguages"
PERTENECE = (200, {"value": [{"solutioncomponentid": "x"}]}, {})


def con_cambio(obj, ruta, valor):
    """Copia de `obj` con el valor en `ruta` (claves e índices) reemplazado, o quitado si `valor` es BORRAR."""
    n = copy.deepcopy(obj)
    nodo = n
    for p in ruta[:-1]:
        nodo = nodo[p]
    if valor is BORRAR:
        del nodo[ruta[-1]]
    else:
        nodo[ruta[-1]] = valor
    return n


# Moneda con un texto DISTINTO en cada idioma: así una herramienta que usara el mismo texto para los dos idiomas, o el de
# un solo idioma, no pasa las pruebas por casualidad.
MONEDA = componente(
    displayname={"1033": "CH - CARP - Currency", "3082": "CH - CARP - Moneda"},
    opciones=[
        {"valor": 159460001, "etiqueta": {"1033": "Cordoba", "3082": "Cordobas"}, "descripcion": "Córdobas nicaragüenses"},
        {"valor": 159460002, "etiqueta": {"1033": "Dollars", "3082": "Dolares"}, "descripcion": "Dólares estadounidenses"},
    ],
)

TIPO_ARREGLO = componente(
    nombre="sanic_carp_ch_tipoarreglo",
    displayname=por_idioma("CH - CARP - Tipo de arreglo"),
    descripcion="Tipo de arreglo según el formato F-NIC-0000052. Lo usa sanic_tipo de Arreglo.",
    opciones=[
        {"valor": 159460001, "etiqueta": por_idioma("Temporal"), "descripcion": "Arreglo temporal"},
        {"valor": 159460002, "etiqueta": por_idioma("Cancelacion"), "descripcion": "Arreglo de cancelación del saldo"},
    ],
)

# Etiquetas del choice `sanic_carp_ch_campomapeable` (diccionario, 3.1): 27 opciones, con dos puntos en el nombre.
CAMPOS_MAPEABLES = [
    "Fecha de formalizacion", "Oficial de arreglo", "Nombre del cliente", "Cedula del cliente", "Correo del cliente",
    "Celular del cliente", "Numero de prestamo o tarjeta", "Tipo de arreglo", "Numero de arreglo", "Saldo inicial",
    "Monto abonado", "Saldo remanente", "Plazo en cuotas", "Cuota mensual", "Total a pagar", "Fecha de inicio",
    "Fecha de finalizacion", "Cuenta a debitar", "Comentarios", "Cuota: numero", "Cuota: fecha de vencimiento",
    "Cuota: monto", "Cuota: fecha de pago", "Cuota: monto pagado", "Total: montos de cuota", "Total: montos pagados", "Moneda",
]


def campo_mapeable():
    opciones = [
        {"valor": 159460000 + i, "etiqueta": por_idioma(texto),
         "descripcion": f"Campo \"{texto}\" que el generador escribe en la celda; según el tipo de mapeo / la columna"}
        for i, texto in enumerate(CAMPOS_MAPEABLES, start=1)
    ]
    return componente(nombre="sanic_carp_ch_campomapeable", displayname=por_idioma("CH - CARP - Campo mapeable"),
                      descripcion="Campos que el generador del formato sabe escribir en una celda.", opciones=opciones)


def pares(etiqueta):
    """[(LanguageCode, Label)] de un `Label` del Web API."""
    return [(ll["LanguageCode"], ll["Label"]) for ll in etiqueta["LocalizedLabels"]]


class Base(unittest.TestCase):
    def construir(self, fabrica_cliente, identidad_dict=None, componente_dict=None, solo_verificar=False, **kw):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "playbook.md")
            escribir_playbook(ruta, identidad_dict, componente_dict)
            return cg.construir(ruta, solo_verificar, fabrica_cliente, **kw)

    def construir_offline(self, identidad_dict=None, componente_dict=None):
        centinela = FabricaCentinela()
        resultado = self.construir(centinela, identidad_dict, componente_dict)
        return resultado, centinela

    def construir_con_cliente(self, cliente, identidad_dict=None, componente_dict=None, solo_verificar=False, **kw):
        return self.construir(lambda: cliente, identidad_dict, componente_dict, solo_verificar, **kw)

    def afirmar_error_offline(self, componente_dict=None, identidad_dict=None, fragmentos=()):
        (estado, _, detalle), centinela = self.construir_offline(identidad_dict, componente_dict)
        self.assertEqual(estado, "error", detalle)
        self.assertFalse(centinela.llamada)
        self.assertNotIn("fallo inesperado", detalle)
        for f in fragmentos:
            self.assertIn(f, detalle)
        return detalle


def cliente_con_entorno(base=1033, provisionados=None, ident=None):
    """Cliente simulado cuyo entorno tiene `base` como idioma base y `provisionados` como idiomas provisionados."""
    ident = IDENTIDAD_VALIDA if ident is None else ident
    cliente = ClienteSimulado()
    cliente.responder("GET", es_ruta_solutions, respuesta_solucion_ok(ident))
    cliente.responder("GET", RUTA_ORG, respuesta_idioma_ok(ident, base=base))
    cliente.responder("GET", RUTA_IDIOMAS, (200, {"RetrieveProvisionedLanguages": list(ident["idiomas"] if provisionados is None else provisionados)}, {}))
    return cliente


def cliente_para_crear(comp, ident=None, base=1033):
    """Entorno donde el choice no existe y, tras el POST, se lee conforme al playbook."""
    ident = IDENTIDAD_VALIDA if ident is None else ident
    cliente = cliente_con_entorno(base=base, ident=ident)
    respuestas = iter([(404, {}, {}), (200, cuerpo_choice_conforme(comp, ident["idiomas"]), {})])
    cliente.responder("GET", ruta_choice(comp["nombre"]), lambda ruta, cuerpo, sol: next(respuestas))
    cliente.responder("POST", "GlobalOptionSetDefinitions", (204, {}, {"OData-EntityId": "https://org/x(1)"}))
    cliente.responder("GET", es_ruta_solutioncomponents, PERTENECE)
    return cliente


def cliente_con_choice(cuerpo, base=1033, ident=None):
    ident = IDENTIDAD_VALIDA if ident is None else ident
    cliente = cliente_con_entorno(base=base, ident=ident)
    cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), (200, cuerpo, {}))
    cliente.responder("GET", es_ruta_solutioncomponents, PERTENECE)
    return cliente


# ---------------------------------------------------------------------------
# Validación previa (offline): ni una sola llamada de red, ni siquiera para
# construir el cliente.
# ---------------------------------------------------------------------------
class ValidacionPreviaOffline(Base):
    """Cada caso afirma, además del estado, una parte del `detalle` que SOLO puede venir del chequeo específico que
    dice ejercitar, y que no salió por la red de contención genérica ('fallo inesperado')."""

    # (descripción, ruta del dato, valor malo, fragmentos que debe nombrar el detalle)
    CASOS = [
        # -- nombre lógico
        ("nombre que no cumple el patrón", ["nombre"], "sanic_carp_choice_moneda", ("sanic_carp_choice_moneda", "no cumple")),
        ("nombre con prefijo equivocado", ["nombre"], "otroprefijo_carp_ch_moneda", ("otroprefijo_carp_ch_moneda", "no cumple")),
        ("nombre con otra abreviatura", ["nombre"], "sanic_otra_ch_moneda", ("sanic_otra_ch_moneda", "no cumple")),
        ("nombre con mayúscula", ["nombre"], "sanic_carp_ch_Moneda", ("sanic_carp_ch_Moneda", "no cumple")),
        ("nombre de más de 95 caracteres", ["nombre"], "sanic_carp_ch_" + "x" * 90, ("95 caracteres",)),
        ("nombre que no es texto", ["nombre"], 5, ("'nombre'", "texto no vacío")),
        # -- displayname: un texto por idioma, nunca una cadena única
        ("displayname en cadena única (formato viejo)", ["displayname"], "CH - CARP - Moneda", ("'displayname'", "objeto")),
        ("displayname que no es objeto ni texto", ["displayname"], None, ("'displayname'", "objeto")),
        ("displayname al que le falta el 3082", ["displayname"], {"1033": "CH - CARP - Moneda"}, ("'displayname'", "faltan ['3082']")),
        ("displayname al que le falta el 1033", ["displayname"], {"3082": "CH - CARP - Moneda"}, ("'displayname'", "faltan ['1033']")),
        ("displayname con un idioma de más", ["displayname"], {"1033": "CH - CARP - Moneda", "3082": "CH - CARP - Moneda", "2058": "CH - CARP - Moneda"},
         ("'displayname'", "sobran ['2058']")),
        ("displayname vacío en 3082", ["displayname", "3082"], "  ", ("'displayname'[3082]", "texto no vacío")),
        ("displayname que no es texto en 1033", ["displayname", "1033"], 7, ("'displayname'[1033]", "texto no vacío")),
        ("displayname sin el patrón CH en 1033", ["displayname", "1033"], "Moneda", ("'displayname'[1033]", "'Moneda'", "no cumple")),
        ("displayname con otra abreviatura solo en 3082", ["displayname", "3082"], "CH - OTRA - Moneda", ("'displayname'[3082]", "CH - OTRA - Moneda")),
        ("displayname que es solo el prefijo en 3082", ["displayname", "3082"], "CH - CARP - ", ("'displayname'[3082]", "no cumple")),
        # -- descripción: prosa, una cadena
        ("descripción en blanco", ["descripcion"], "  ", ("'descripcion'", "obligatoria")),
        ("descripción que no es texto", ["descripcion"], None, ("'descripcion'", "tiene que ser texto")),
        ("descripción por idioma (se escribe una sola vez)", ["descripcion"], por_idioma("Moneda del arreglo"), ("'descripcion'", "tiene que ser texto")),
        # -- opciones
        ("opciones vacías", ["opciones"], [], ("'opciones'", "no vacía")),
        ("opciones que no es lista", ["opciones"], "COR", ("'opciones'", "no vacía")),
        ("opción que no es objeto", ["opciones", 0], "COR", ("opciones[0]", "exactamente las claves")),
        ("opción sin la clave descripcion", ["opciones", 0, "descripcion"], BORRAR, ("opciones[0]", "exactamente las claves")),
        ("opción con una clave de más", ["opciones", 0, "color"], "rojo", ("opciones[0]", "exactamente las claves")),
        ("valor de opción como texto", ["opciones", 0, "valor"], "159460001", ("opciones[0].valor", "entero")),
        ("valor de opción booleano", ["opciones", 0, "valor"], True, ("opciones[0].valor", "entero")),
        ("valor de opción decimal", ["opciones", 0, "valor"], 159460001.0, ("opciones[0].valor", "entero")),
        ("etiqueta en cadena única (formato viejo)", ["opciones", 0, "etiqueta"], "Cordobas", ("opciones[0].etiqueta", "objeto")),
        ("etiqueta a la que le falta el 3082", ["opciones", 0, "etiqueta"], {"1033": "Cordobas"}, ("opciones[0].etiqueta", "faltan ['3082']")),
        ("etiqueta con un idioma de más", ["opciones", 1, "etiqueta"], {"1033": "Dolares", "3082": "Dolares", "2058": "Dolares"}, ("opciones[1].etiqueta", "sobran ['2058']")),
        ("etiqueta vacía en 1033", ["opciones", 1, "etiqueta", "1033"], "", ("opciones[1].etiqueta[1033]", "texto no vacío")),
        ("descripción de opción por idioma", ["opciones", 0, "descripcion"], por_idioma("Córdobas"), ("opciones[0].descripcion", "texto")),
        ("descripción de opción que no es texto", ["opciones", 0, "descripcion"], None, ("opciones[0].descripcion", "texto")),
        # -- repetidos: las etiquetas se comparan DENTRO de cada idioma
        ("etiquetas repetidas en 1033", ["opciones"],
         [{"valor": 159460001, "etiqueta": {"1033": "Cash", "3082": "Efectivo"}, "descripcion": ""},
          {"valor": 159460002, "etiqueta": {"1033": "Cash", "3082": "Tarjeta"}, "descripcion": ""}], ("etiquetas repetidas", "idioma 1033", "Cash")),
        ("etiquetas repetidas solo en 3082", ["opciones"],
         [{"valor": 159460001, "etiqueta": {"1033": "Cash", "3082": "Efectivo"}, "descripcion": ""},
          {"valor": 159460002, "etiqueta": {"1033": "Card", "3082": "Efectivo"}, "descripcion": ""}], ("etiquetas repetidas", "idioma 3082", "Efectivo")),
        ("valores repetidos", ["opciones"],
         [{"valor": 159460001, "etiqueta": por_idioma("Temporal"), "descripcion": ""},
          {"valor": 159460001, "etiqueta": por_idioma("Cancelacion"), "descripcion": ""}], ("valores repetidos",)),
        # -- bloque del componente
        ("clave de más en el bloque (el lcid viejo)", ["lcid"], 1033, ("lcid", "sobran")),
        ("clave faltante en el bloque", ["descripcion"], BORRAR, ("descripcion", "faltan")),
        ("tipo de otro playbook", ["tipo"], "tabla-y-columnas", ("tabla-y-columnas",)),
    ]

    def test_cada_dato_invalido_es_error_nombra_el_campo_y_no_toca_la_red(self):
        for descripcion, ruta, valor, fragmentos in self.CASOS:
            with self.subTest(descripcion):
                self.afirmar_error_offline(con_cambio(COMPONENTE_VALIDO, ruta, valor), fragmentos=fragmentos)

    def test_las_etiquetas_iguales_en_idiomas_distintos_no_son_repetidas(self):
        """Una etiqueta de una opción en 1033 puede ser igual a la de OTRA opción en 3082: se comparan dentro de cada idioma."""
        cruzadas = componente(opciones=[
            {"valor": 159460001, "etiqueta": {"1033": "Temporal", "3082": "Cancelacion"}, "descripcion": ""},
            {"valor": 159460002, "etiqueta": {"1033": "Cancelacion", "3082": "Temporal"}, "descripcion": ""},
        ])
        cg.validar_json_componente(cruzadas, IDENTIDAD_VALIDA)  # no debe lanzar
        cg.validar_nombres(cruzadas, IDENTIDAD_VALIDA)

    def test_valor_fuera_del_rango_declarado_en_identidad_es_bloqueado(self):
        opciones = [{"valor": 100000001, "etiqueta": por_idioma("X"), "descripcion": ""}]
        (estado, _, detalle), centinela = self.construir_offline(componente_dict=componente(opciones=opciones))
        self.assertEqual(estado, "bloqueado")
        self.assertFalse(centinela.llamada)
        self.assertIn("100000001", detalle)
        self.assertIn("159460000", detalle)

    def test_la_descripcion_si_lleva_tildes_y_los_nombres_no(self):
        cg.validar_json_componente(COMPONENTE_VALIDO, IDENTIDAD_VALIDA)
        cg.validar_nombres(COMPONENTE_VALIDO, IDENTIDAD_VALIDA)  # la descripción del choice y la de cada opción tienen tildes
        self.assertTrue(any(ord(c) > 126 for c in COMPONENTE_VALIDO["descripcion"]))


class ValidacionDeIdentidad(Base):
    CASOS = [
        ("clave de más", lambda i: dict(i, extra="x"), ("extra", "sobran")),
        ("lcid viejo en lugar de idiomas", lambda i: dict({k: v for k, v in i.items() if k != "idiomas"}, lcid=1033), ("faltan ['idiomas']", "sobran ['lcid']")),
        ("lcid viejo además de idiomas", lambda i: dict(i, lcid=1033), ("lcid", "sobran")),
        ("idiomas como texto", lambda i: dict(i, idiomas="1033,3082"), ("'idiomas'", "lista")),
        ("idiomas vacía", lambda i: dict(i, idiomas=[]), ("'idiomas'", "no puede estar vacía")),
        ("idiomas sin el 1033", lambda i: dict(i, idiomas=[3082]), ("'idiomas'", "no incluye el 1033")),
        ("idiomas con repetidos", lambda i: dict(i, idiomas=[1033, 1033]), ("'idiomas'", "repetidos")),
        ("idioma que es texto", lambda i: dict(i, idiomas=[1033, "3082"]), ("'idiomas'", "LCID entero positivo")),
        ("prefijo_opciones como texto", lambda i: dict(i, prefijo_opciones="15946"), ("prefijo_opciones", "entero")),
        ("prefijo_opciones decimal", lambda i: dict(i, prefijo_opciones=15946.0), ("prefijo_opciones", "entero")),
        ("prefijo_opciones booleano", lambda i: dict(i, prefijo_opciones=True), ("prefijo_opciones", "entero")),
    ]

    def test_cada_dato_invalido_de_identidad_es_error_y_no_toca_la_red(self):
        for descripcion, cambiar, fragmentos in self.CASOS:
            with self.subTest(descripcion):
                self.afirmar_error_offline(identidad_dict=cambiar(copy.deepcopy(IDENTIDAD_VALIDA)), fragmentos=fragmentos)

    def test_los_idiomas_de_identidad_y_los_del_componente_tienen_que_coincidir(self):
        # el playbook trae los dos idiomas y la identidad declara uno: sobra el 3082
        self.afirmar_error_offline(identidad_dict=identidad(idiomas=[1033]), fragmentos=("'displayname'", "sobran ['3082']"))
        # la identidad declara un idioma más que el componente: falta el 2058
        self.afirmar_error_offline(identidad_dict=identidad(idiomas=[1033, 3082, 2058]), fragmentos=("'displayname'", "faltan ['2058']"))

    def test_un_solo_idioma_declarado_basta_si_el_componente_lo_escribe_asi(self):
        solo_ingles = componente(displayname={"1033": "CH - CARP - Moneda"}, opciones=[
            {"valor": 159460001, "etiqueta": {"1033": "Cordobas"}, "descripcion": "Córdobas"}])
        ident = identidad(idiomas=[1033])
        cg.validar_json_componente(solo_ingles, ident)
        cg.validar_nombres(solo_ingles, ident)
        payload = cg.construir_payload(solo_ingles, ident)
        self.assertEqual(pares(payload["DisplayName"]), [(1033, "CH - CARP - Moneda")])
        self.assertEqual(pares(payload["Options"][0]["Label"]), [(1033, "Cordobas")])


class SinTildesEnNombresVisibles(Base):
    """BP-PP-197 en cada idioma y sin excepciones heredadas: todo nombre visible con tilde se rechaza, y el detalle dice en
    qué campo e idioma está."""

    CAMPOS = [
        (["displayname", "1033"], "'displayname'[1033]"),
        (["displayname", "3082"], "'displayname'[3082]"),
        (["opciones", 0, "etiqueta", "1033"], "opciones[0].etiqueta[1033]"),
        (["opciones", 1, "etiqueta", "3082"], "opciones[1].etiqueta[3082]"),
    ]

    def test_tilde_o_enie_en_cada_nombre_visible_de_cada_idioma(self):
        for ruta, donde in self.CAMPOS:
            for sufijo in (" técnico", " año"):
                with self.subTest(f"{ruta} {sufijo}"):
                    actual = COMPONENTE_VALIDO
                    for p in ruta:
                        actual = actual[p]
                    self.afirmar_error_offline(con_cambio(COMPONENTE_VALIDO, ruta, actual + sufijo), fragmentos=("BP-PP-197", donde))

    def test_las_palabras_del_diccionario_con_su_tilde_se_rechazan(self):
        """Las etiquetas del diccionario van sin tilde; con ella no hay ninguna excepción que las salve."""
        casos = [("Cordobas", "Córdobas"), ("Dolares", "Dólares"), ("Cancelacion", "Cancelación"), ("Historica", "Histórica"),
                 ("Respuesta automatica", "Respuesta automática"), ("Numero de arreglo", "Número de arreglo")]
        for sin, con in casos:
            with self.subTest(con):
                comp = con_cambio(TIPO_ARREGLO, ["opciones", 0, "etiqueta", "3082"], con)
                self.afirmar_error_offline(comp, fragmentos=("BP-PP-197", con))
        comp = con_cambio(TIPO_ARREGLO, ["displayname", "1033"], "CH - CARP - Resultado de conversación")
        self.afirmar_error_offline(comp, fragmentos=("BP-PP-197", "ó"))


class ExcepcionImprevistaEnFaseSinRed(Base):
    """La fase sin red (leer y validar el playbook) tiene que tener su propia red de contención: cualquier excepción, no
    solo ErrorPlaybook/Bloqueado/OSError, termina en 'error' y nunca escapa como traza."""

    def test_typeerror_en_validacion_offline_termina_en_error_sin_traza(self):
        with mock.patch.object(cg, "validar_nombres", side_effect=TypeError("forma de dato inesperada")):
            (estado, _, detalle), centinela = self.construir_offline()
        self.assertEqual(estado, "error")
        self.assertFalse(centinela.llamada)
        self.assertIn("TypeError", detalle)
        self.assertIn("fallo inesperado", detalle)
        self.assertIn("validar los nombres", detalle)


# ---------------------------------------------------------------------------
# Funciones puras: el cuerpo del POST y la comparación contra el entorno.
# ---------------------------------------------------------------------------
class ConstruirPayload(unittest.TestCase):
    def test_cabecera_del_choice(self):
        payload = cg.construir_payload(COMPONENTE_VALIDO, IDENTIDAD_VALIDA)
        self.assertEqual(payload["@odata.type"], "Microsoft.Dynamics.CRM.OptionSetMetadata")
        self.assertEqual(payload["Name"], COMPONENTE_VALIDO["nombre"])
        self.assertEqual(payload["OptionSetType"], "Picklist")
        self.assertIs(payload["IsGlobal"], True)
        self.assertEqual([o["Value"] for o in payload["Options"]], [159460001, 159460002])

    def test_cada_texto_viaja_en_cada_idioma_y_en_el_orden_de_idiomas(self):
        p = cg.construir_payload(MONEDA, IDENTIDAD_VALIDA)
        desc = "Moneda en la que se expresan los montos de un arreglo y de sus cuotas: córdobas o dólares."
        casos = [
            ("DisplayName", p["DisplayName"], [(1033, "CH - CARP - Currency"), (3082, "CH - CARP - Moneda")]),
            ("Description", p["Description"], [(1033, desc), (3082, desc)]),
            ("Options[0].Label", p["Options"][0]["Label"], [(1033, "Cordoba"), (3082, "Cordobas")]),
            ("Options[1].Label", p["Options"][1]["Label"], [(1033, "Dollars"), (3082, "Dolares")]),
            ("Options[0].Description", p["Options"][0]["Description"], [(1033, "Córdobas nicaragüenses"), (3082, "Córdobas nicaragüenses")]),
            ("Options[1].Description", p["Options"][1]["Description"], [(1033, "Dólares estadounidenses"), (3082, "Dólares estadounidenses")]),
        ]
        for que, etiqueta, esperado in casos:
            with self.subTest(que):
                self.assertEqual(pares(etiqueta), esperado)
                self.assertEqual(etiqueta["@odata.type"], "Microsoft.Dynamics.CRM.Label")
                self.assertTrue(all(ll["@odata.type"] == "Microsoft.Dynamics.CRM.LocalizedLabel" for ll in etiqueta["LocalizedLabels"]))

    def test_una_opcion_sin_descripcion_manda_cadena_vacia_en_cada_idioma(self):
        comp = con_cambio(MONEDA, ["opciones", 0, "descripcion"], "")
        p = cg.construir_payload(comp, IDENTIDAD_VALIDA)
        self.assertEqual(pares(p["Options"][0]["Description"]), [(1033, ""), (3082, "")])

    def test_con_tres_idiomas_viajan_los_tres(self):
        ident = identidad(idiomas=[1033, 3082, 2058])
        comp = componente(displayname=por_idioma("CH - CARP - Moneda", [1033, 3082, 2058]), opciones=[
            {"valor": 159460001, "etiqueta": {"1033": "Cordobas", "3082": "Cordobas", "2058": "Cordobas"}, "descripcion": "x"}])
        p = cg.construir_payload(comp, ident)
        self.assertEqual([c for c, _ in pares(p["DisplayName"])], [1033, 3082, 2058])
        self.assertEqual([c for c, _ in pares(p["Options"][0]["Label"])], [1033, 3082, 2058])
        self.assertEqual([c for c, _ in pares(p["Options"][0]["Description"])], [1033, 3082, 2058])
        self.assertEqual([c for c, _ in pares(p["Description"])], [1033, 3082, 2058])


def fijar(etiqueta, lcid, texto):
    for ll in etiqueta["LocalizedLabels"]:
        if ll["LanguageCode"] == lcid:
            ll["Label"] = texto
            return
    raise AssertionError(f"no hay etiqueta {lcid}")


def quitar(etiqueta, lcid):
    etiqueta["LocalizedLabels"] = [ll for ll in etiqueta["LocalizedLabels"] if ll["LanguageCode"] != lcid]


class CompararContraPlaybook(unittest.TestCase):
    # (nombre en el mensaje, de dónde se saca la etiqueta del cuerpo, texto esperado del playbook por idioma)
    DESC_MONEDA = "Moneda en la que se expresan los montos de un arreglo y de sus cuotas: córdobas o dólares."
    CAMPOS = [
        ("DisplayName", lambda c: c["DisplayName"], {1033: "CH - CARP - Currency", 3082: "CH - CARP - Moneda"}),
        ("Description", lambda c: c["Description"], {1033: DESC_MONEDA, 3082: DESC_MONEDA}),
        ("opciones[0].etiqueta", lambda c: c["Options"][0]["Label"], {1033: "Cordoba", 3082: "Cordobas"}),
        ("opciones[1].etiqueta", lambda c: c["Options"][1]["Label"], {1033: "Dollars", 3082: "Dolares"}),
        ("opciones[0].descripcion", lambda c: c["Options"][0]["Description"], {1033: "Córdobas nicaragüenses", 3082: "Córdobas nicaragüenses"}),
        ("opciones[1].descripcion", lambda c: c["Options"][1]["Description"], {1033: "Dólares estadounidenses", 3082: "Dólares estadounidenses"}),
    ]

    def comparar(self, cuerpo, comp=MONEDA):
        return cg.comparar_contra_playbook(cuerpo, comp, IDENTIDAD_VALIDA)

    def test_coincide_exactamente_sin_diferencias(self):
        self.assertEqual(self.comparar(cuerpo_choice_conforme(MONEDA)), [])

    def test_una_descripcion_ausente_en_el_entorno_se_lee_como_vacia(self):
        comp = con_cambio(MONEDA, ["opciones", 1, "descripcion"], "")
        cuerpo = cuerpo_choice_conforme(comp)
        cuerpo["Options"][1]["Description"] = None
        self.assertEqual(self.comparar(cuerpo, comp), [])

    def test_banderas_y_estructura(self):
        casos = [
            ("Name", lambda c: c.update(Name="sanic_carp_ch_otra"), "Name: entorno='sanic_carp_ch_otra'"),
            ("IsManaged", lambda c: c.update(IsManaged=True), "IsManaged"),
            ("IsGlobal", lambda c: c.update(IsGlobal=False), "IsGlobal"),
            ("OptionSetType", lambda c: c.update(OptionSetType="Boolean"), "OptionSetType"),
            ("valor de opción", lambda c: c["Options"][0].update(Value=999999999), "opciones[0].Value"),
            ("cantidad de opciones", lambda c: c.update(Options=c["Options"][:1]), "cantidad de opciones"),
            ("orden de las opciones", lambda c: c.update(Options=list(reversed(c["Options"]))), "opciones[0].Value"),
        ]
        for nombre, mutar, fragmento in casos:
            with self.subTest(nombre):
                cuerpo = cuerpo_choice_conforme(MONEDA)
                mutar(cuerpo)
                diffs = self.comparar(cuerpo)
                self.assertTrue(any(fragmento in d for d in diffs), diffs)

    def test_un_texto_distinto_en_un_idioma_es_una_diferencia_de_ese_idioma(self):
        for que, sacar, esperados in self.CAMPOS:
            for lcid in (1033, 3082):
                with self.subTest(f"{que}[{lcid}]"):
                    cuerpo = cuerpo_choice_conforme(MONEDA)
                    fijar(sacar(cuerpo), lcid, "otro texto")
                    diffs = self.comparar(cuerpo)
                    self.assertEqual(diffs, [f"{que}[{lcid}]: entorno='otro texto' playbook={esperados[lcid]!r}"])

    def test_el_entorno_con_la_etiqueta_en_un_solo_idioma_es_una_diferencia_del_idioma_que_falta(self):
        for que, sacar, esperados in self.CAMPOS:
            for faltante in (1033, 3082):
                with self.subTest(f"{que} sin {faltante}"):
                    cuerpo = cuerpo_choice_conforme(MONEDA)
                    quitar(sacar(cuerpo), faltante)
                    diffs = self.comparar(cuerpo)
                    self.assertEqual(diffs, [f"{que}[{faltante}]: entorno=None playbook={esperados[faltante]!r}"])

    def test_una_etiqueta_de_mas_en_un_idioma_que_el_playbook_no_declara_es_una_diferencia(self):
        for que, sacar, _ in self.CAMPOS:
            with self.subTest(que):
                cuerpo = cuerpo_choice_conforme(MONEDA)
                sacar(cuerpo)["LocalizedLabels"].append({"Label": "extra", "LanguageCode": 2058})
                diffs = self.comparar(cuerpo)
                self.assertEqual(len(diffs), 1, diffs)
                self.assertIn(que, diffs[0])
                self.assertIn("no declara", diffs[0])
                self.assertIn("2058", diffs[0])

    def test_se_verifican_exactamente_los_idiomas_declarados_sean_uno_o_tres(self):
        ident_uno = identidad(idiomas=[1033])
        uno = componente(displayname={"1033": "CH - CARP - Moneda"}, opciones=[{"valor": 159460001, "etiqueta": {"1033": "Cordobas"}, "descripcion": "x"}])
        self.assertEqual(cg.comparar_contra_playbook(cuerpo_choice_conforme(uno, [1033]), uno, ident_uno), [])
        tres_idiomas = [1033, 3082, 2058]
        ident_tres = identidad(idiomas=tres_idiomas)
        tres = componente(displayname=por_idioma("CH - CARP - Moneda", tres_idiomas), opciones=[
            {"valor": 159460001, "etiqueta": por_idioma("Cordobas", tres_idiomas), "descripcion": "x"}])
        conforme = cuerpo_choice_conforme(tres, tres_idiomas)
        self.assertEqual(cg.comparar_contra_playbook(conforme, tres, ident_tres), [])
        for que, sacar in (("DisplayName", lambda c: c["DisplayName"]), ("opciones[0].etiqueta", lambda c: c["Options"][0]["Label"]),
                           ("opciones[0].descripcion", lambda c: c["Options"][0]["Description"])):
            with self.subTest(que):
                cuerpo = copy.deepcopy(conforme)
                fijar(sacar(cuerpo), 2058, "otro")
                diffs = cg.comparar_contra_playbook(cuerpo, tres, ident_tres)
                self.assertEqual(len(diffs), 1, diffs)
                self.assertTrue(diffs[0].startswith(f"{que}[2058]: entorno='otro'"), diffs)

    def test_el_orden_de_las_etiquetas_dentro_del_label_no_importa(self):
        cuerpo = cuerpo_choice_conforme(MONEDA)
        cuerpo["DisplayName"]["LocalizedLabels"].reverse()
        cuerpo["Options"][1]["Label"]["LocalizedLabels"].reverse()
        self.assertEqual(self.comparar(cuerpo), [])

    def test_una_descripcion_de_opcion_vacia_con_texto_en_el_entorno_es_diferencia(self):
        comp = con_cambio(MONEDA, ["opciones", 0, "descripcion"], "")
        cuerpo = cuerpo_choice_conforme(comp)
        fijar(cuerpo["Options"][0]["Description"], 3082, "algo")
        self.assertEqual(self.comparar(cuerpo, comp), ["opciones[0].descripcion[3082]: entorno='algo' playbook=''"])


# ---------------------------------------------------------------------------
# Precondiciones contra "el entorno" (cliente simulado). Ninguna debe llegar
# a escribir nada.
# ---------------------------------------------------------------------------
class PrecondicionesContraEntorno(Base):
    @staticmethod
    def solucion(mutar):
        est, cuerpo, cab = respuesta_solucion_ok()
        mutar(cuerpo["value"][0])
        return (est, cuerpo, cab)

    def test_cada_precondicion_de_negocio_incumplida_es_bloqueado_y_no_escribe(self):
        casos = [
            ("la solución no existe", (200, {"value": []}, {}), "no existe"),
            ("la solución es managed", self.solucion(lambda f: f.update(ismanaged=True)), "managed"),
            ("publisher con otro unique name", self.solucion(lambda f: f["publisherid"].update(uniquename="sanic")), "publisher"),
            ("prefijo de texto distinto", self.solucion(lambda f: f["publisherid"].update(customizationprefix="otro")), "prefijo del publisher"),
            ("prefijo de opciones distinto", self.solucion(lambda f: f["publisherid"].update(customizationoptionvalueprefix=10000)), "prefijo de opciones"),
        ]
        for nombre, respuesta, fragmento in casos:
            with self.subTest(nombre):
                cliente = ClienteSimulado()
                cliente.responder("GET", es_ruta_solutions, respuesta)
                estado, _, detalle = self.construir_con_cliente(cliente)
                self.assertEqual(estado, "bloqueado", detalle)
                self.assertIn(fragmento, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_idioma_base_del_entorno_que_el_playbook_no_declara_es_bloqueado(self):
        cliente = cliente_con_entorno(base=2058, provisionados=[1033, 3082, 2058])
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("idioma base del entorno (2058)", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_idioma_del_playbook_no_provisionado_es_bloqueado(self):
        for provisionados in ([1033], [2058], [3082]):
            with self.subTest(provisionados):
                cliente = cliente_con_entorno(base=provisionados[0], provisionados=provisionados)
                estado, _, detalle = self.construir_con_cliente(cliente)
                self.assertEqual(estado, "bloqueado", detalle)
                faltan = [i for i in IDENTIDAD_VALIDA["idiomas"] if i not in provisionados]
                self.assertIn(f"{faltan}", detalle)
                self.assertIn("no están provisionados", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_el_idioma_base_puede_ser_el_3082_si_el_playbook_declara_los_dos(self):
        """Entorno con base en español: es el caso normal de un cliente de habla hispana."""
        cliente = cliente_para_crear(MONEDA, base=3082)
        estado, _, detalle = self.construir_con_cliente(cliente, componente_dict=MONEDA)
        self.assertEqual(estado, "creado", detalle)
        cliente = cliente_con_choice(cuerpo_choice_conforme(COMPONENTE_VALIDO), base=3082)
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "ya_existia", detalle)


class PrecondicionesHttpYFormaInesperados(Base):
    """Un HTTP inesperado o un 200 con forma rara en cualquier consulta es 'no se pudo averiguar' (error), nunca una
    respuesta de negocio (`bloqueado`, `difiere`...). `bloqueado` queda solo para un 200 cuyo contenido dice que la
    precondición no se cumple. Las rutas del camino feliz van todas configuradas aunque la ejecución deba cortar antes."""

    def correr(self, matcher, respuesta, solo_verificar=False):
        cliente = ClienteSimulado()
        cliente.responder("GET", matcher, respuesta)  # la primera regla que coincide gana
        armar_cliente_precondiciones_ok(cliente)
        cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), (200, cuerpo_choice_conforme(), {}))
        cliente.responder("GET", es_ruta_solutioncomponents, PERTENECE)
        estado, _, detalle = self.construir_con_cliente(cliente, solo_verificar=solo_verificar)
        self.assertFalse(cliente.hubo_escritura())
        return estado, detalle

    CONSULTAS = [
        ("solutions", es_ruta_solutions, "GET solutions"),
        ("organizations", RUTA_ORG, "GET organizations"),
        ("RetrieveProvisionedLanguages", RUTA_IDIOMAS, "GET RetrieveProvisionedLanguages"),
        ("existencia del choice", ruta_choice(COMPONENTE_VALIDO["nombre"]), "GET GlobalOptionSetDefinitions"),
        ("solutioncomponents", es_ruta_solutioncomponents, "GET solutioncomponents"),
    ]

    def test_http_inesperado_en_cada_consulta_es_error_y_nombra_la_consulta(self):
        for nombre, matcher, consulta in self.CONSULTAS:
            for http in (401, 500):
                with self.subTest(f"{nombre} {http}"):
                    estado, detalle = self.correr(matcher, (http, {"error": "boom"}, {}))
                    self.assertEqual(estado, "error", detalle)
                    self.assertIn(f"HTTP {http}", detalle)
                    self.assertIn(consulta, detalle)
                    self.assertNotIn("fallo inesperado", detalle)

    def test_200_con_forma_inesperada_es_error_y_nombra_consulta_y_campo(self):
        _, sol, _ = respuesta_solucion_ok()
        sin_publisher = copy.deepcopy(sol)
        del sin_publisher["value"][0]["publisherid"]
        sin_solutionid = copy.deepcopy(sol)
        del sin_solutionid["value"][0]["solutionid"]
        casos = [
            ("solutions sin value", es_ruta_solutions, (200, {"otracosa": []}, {}), "GET solutions", "'value'"),
            ("solutions sin publisherid expandido", es_ruta_solutions, (200, sin_publisher, {}), "GET solutions", "publisherid"),
            ("solutions sin solutionid", es_ruta_solutions, (200, sin_solutionid, {}), "GET solutions", "solutionid"),
            ("organizations con value vacío", RUTA_ORG, (200, {"value": []}, {}), "GET organizations", "'value'"),
            ("organizations sin languagecode", RUTA_ORG, (200, {"value": [{}]}, {}), "GET organizations", "languagecode"),
            ("idiomas sin la propiedad", RUTA_IDIOMAS, (200, {"algo_distinto": []}, {}), "RetrieveProvisionedLanguages", "RetrieveProvisionedLanguages"),
            ("idiomas como texto", RUTA_IDIOMAS, (200, {"RetrieveProvisionedLanguages": "1033"}, {}), "RetrieveProvisionedLanguages", "RetrieveProvisionedLanguages"),
            ("idiomas con un texto adentro", RUTA_IDIOMAS, (200, {"RetrieveProvisionedLanguages": [1033, "3082"]}, {}), "RetrieveProvisionedLanguages", "RetrieveProvisionedLanguages[1]"),
            ("idiomas vacía", RUTA_IDIOMAS, (200, {"RetrieveProvisionedLanguages": []}, {}), "RetrieveProvisionedLanguages", "RetrieveProvisionedLanguages"),
            ("choice que no es objeto", ruta_choice(COMPONENTE_VALIDO["nombre"]), (200, ["no", "es", "objeto"], {}), "GET GlobalOptionSetDefinitions", "cuerpo"),
            ("choice sin MetadataId", ruta_choice(COMPONENTE_VALIDO["nombre"]), (200, {k: v for k, v in cuerpo_choice_conforme().items() if k != "MetadataId"}, {}),
             "GET GlobalOptionSetDefinitions", "MetadataId"),
            ("solutioncomponents sin value", es_ruta_solutioncomponents, (200, {"otracosa": []}, {}), "GET solutioncomponents", "'value'"),
        ]
        for nombre, matcher, respuesta, consulta, campo in casos:
            with self.subTest(nombre):
                estado, detalle = self.correr(matcher, respuesta)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(consulta, detalle)
                self.assertIn(campo, detalle)
                self.assertNotIn("fallo inesperado", detalle)


# ---------------------------------------------------------------------------
# Los tres caminos de la verificación (creado / ya existía / --solo-verificar)
# ---------------------------------------------------------------------------
class CaminoCreacion(Base):
    def test_creacion_feliz_un_solo_post_con_la_cabecera_de_solucion_y_todos_los_idiomas(self):
        cliente = cliente_para_crear(MONEDA)
        estado, comp, detalle = self.construir_con_cliente(cliente, componente_dict=MONEDA)

        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(comp, MONEDA["nombre"])
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(len(posts), 1)
        self.assertEqual(posts[0]["solucion"], IDENTIDAD_VALIDA["solucion"])
        self.assertEqual(posts[0]["cuerpo"], cg.construir_payload(MONEDA, IDENTIDAD_VALIDA))
        self.assertEqual(pares(posts[0]["cuerpo"]["DisplayName"]), [(1033, "CH - CARP - Currency"), (3082, "CH - CARP - Moneda")])

    def test_204_con_relectura_que_difiere_no_reintenta_crear_y_es_error(self):
        cliente = cliente_con_entorno()
        respuestas = iter([(404, {}, {}), (200, cuerpo_choice_conforme(is_managed=True), {})])
        cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), lambda ruta, cuerpo, sol: next(respuestas))
        cliente.responder("POST", "GlobalOptionSetDefinitions", (204, {}, {"OData-EntityId": "https://org/x(1)"}))
        cliente.responder("GET", es_ruta_solutioncomponents, PERTENECE)
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "error")
        self.assertIn("no coincide", detalle)
        self.assertIn("IsManaged", detalle)
        self.assertEqual(len([l for l in cliente.llamadas if l["metodo"] == "POST"]), 1, "no debe reintentar crear ante una relectura que difiere")

    def test_204_con_relectura_que_trae_un_idioma_menos_es_error(self):
        """El 204 no verifica nada: si al releer falta la etiqueta de un idioma, no se informa creado."""
        cuerpo = cuerpo_choice_conforme(MONEDA)
        quitar(cuerpo["DisplayName"], 3082)
        cliente = cliente_con_entorno()
        respuestas = iter([(404, {}, {}), (200, cuerpo, {})])
        cliente.responder("GET", ruta_choice(MONEDA["nombre"]), lambda ruta, cuerpo, sol: next(respuestas))
        cliente.responder("POST", "GlobalOptionSetDefinitions", (204, {}, {}))
        cliente.responder("GET", es_ruta_solutioncomponents, PERTENECE)
        estado, _, detalle = self.construir_con_cliente(cliente, componente_dict=MONEDA)
        self.assertEqual(estado, "error")
        self.assertIn("no coincide", detalle)
        self.assertIn("DisplayName[3082]", detalle)

    def test_204_pero_no_aparece_al_releer_es_error(self):
        cliente = cliente_con_entorno()
        cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), (404, {}, {}))
        cliente.responder("POST", "GlobalOptionSetDefinitions", (204, {}, {}))
        cliente.responder("GET", es_ruta_solutioncomponents, PERTENECE)
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "error")
        self.assertIn("no aparece al releer", detalle)

    def test_post_que_no_devuelve_204_es_error_sin_reintentar(self):
        cliente = cliente_con_entorno()
        cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), (404, {}, {}))
        cliente.responder("POST", "GlobalOptionSetDefinitions", (400, {"error": "algo salió mal"}, {}))
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "error")
        self.assertIn("HTTP 400", detalle)
        self.assertIn("la creación falló", detalle)
        self.assertEqual(len([l for l in cliente.llamadas if l["metodo"] == "POST"]), 1)


class CaminoYaExistiaYDifiere(Base):
    def test_ya_existia_coincide_cero_escritura(self):
        cliente = cliente_con_choice(cuerpo_choice_conforme(MONEDA))
        estado, _, detalle = self.construir_con_cliente(cliente, componente_dict=MONEDA)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_existe_pero_difiere_en_un_idioma_cero_escritura_y_nombra_idioma(self):
        for lcid in (1033, 3082):
            with self.subTest(lcid):
                cuerpo = cuerpo_choice_conforme(MONEDA)
                fijar(cuerpo["DisplayName"], lcid, "CH - CARP - Otra cosa")
                cliente = cliente_con_choice(cuerpo)
                estado, _, detalle = self.construir_con_cliente(cliente, componente_dict=MONEDA)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(f"DisplayName[{lcid}]: entorno='CH - CARP - Otra cosa'", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_existe_y_coincide_pero_no_pertenece_a_la_solucion_es_difiere(self):
        cliente = cliente_con_entorno()
        cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), (200, cuerpo_choice_conforme(), {}))
        cliente.responder("GET", es_ruta_solutioncomponents, (200, {"value": []}, {}))
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "difiere")
        self.assertIn("pertenencia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_diff_junta_todas_las_diferencias_de_todos_los_idiomas(self):
        cuerpo = cuerpo_choice_conforme(MONEDA)
        fijar(cuerpo["DisplayName"], 1033, "a")
        fijar(cuerpo["Options"][1]["Label"], 3082, "b")
        cliente = cliente_con_choice(cuerpo)
        estado, _, detalle = self.construir_con_cliente(cliente, componente_dict=MONEDA)
        self.assertEqual(estado, "difiere")
        self.assertIn("DisplayName[1033]", detalle)
        self.assertIn("opciones[1].etiqueta[3082]", detalle)


class CaminoSoloVerificar(Base):
    def test_no_existe_es_error_y_nunca_hay_post_posible(self):
        cliente = cliente_con_entorno()
        cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), (404, {}, {}))
        estado, _, detalle = self.construir_con_cliente(cliente, solo_verificar=True)
        self.assertEqual(estado, "error")
        self.assertIn("no existe en el entorno", detalle)
        self.assertNotIn("POST", cliente.metodos_llamados())

    def test_coincide_es_ya_existia(self):
        cliente = cliente_con_choice(cuerpo_choice_conforme())
        estado, _, detalle = self.construir_con_cliente(cliente, solo_verificar=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_difiere_es_difiere(self):
        cliente = cliente_con_choice(cuerpo_choice_conforme(is_managed=True))
        estado, _, detalle = self.construir_con_cliente(cliente, solo_verificar=True)
        self.assertEqual(estado, "difiere")
        self.assertIn("IsManaged", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_etiqueta_de_mas_en_un_idioma_no_declarado_es_difiere(self):
        cuerpo = cuerpo_choice_conforme()
        cuerpo["Options"][0]["Label"]["LocalizedLabels"].append({"Label": "Cordobas", "LanguageCode": 2058})
        cliente = cliente_con_choice(cuerpo)
        estado, _, detalle = self.construir_con_cliente(cliente, solo_verificar=True)
        self.assertEqual(estado, "difiere")
        self.assertIn("opciones[0].etiqueta tiene etiquetas en un idioma que el playbook no declara", detalle)
        self.assertIn("2058", detalle)


class ExcepcionesDelCliente(Base):
    def test_fabrica_cliente_lanza_excepcion_termina_en_error_sin_exponer_el_detalle(self):
        fabrica = FabricaFalla(TimeoutError("timed out talking to login.microsoftonline.com with secret=abc123"))
        estado, _, detalle = self.construir(fabrica)
        self.assertEqual(estado, "error")
        self.assertEqual(detalle, cg.MENSAJE_FALLO_CLIENTE)
        self.assertNotIn("abc123", detalle)

    def test_llamada_de_red_lanza_excepcion_en_medio_de_las_precondiciones(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", es_ruta_solutions, TimeoutError("se cayó la red"))
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "error")
        self.assertIn("TimeoutError", detalle)
        self.assertIn("fallo inesperado", detalle)
        self.assertIn("GET solutions", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_excepcion_durante_la_verificacion_final_no_deja_traza_sin_manejar(self):
        cliente = cliente_con_entorno()
        cliente.responder("GET", ruta_choice(COMPONENTE_VALIDO["nombre"]), ConnectionError("se cortó a mitad de camino"))
        estado, _, detalle = self.construir_con_cliente(cliente)
        self.assertEqual(estado, "error")
        self.assertIn("ConnectionError", detalle)
        self.assertIn("GlobalOptionSetDefinitions", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_playbook_inexistente_es_error(self):
        estado, _, detalle = cg.construir(os.path.join(tempfile.gettempdir(), "no-existe-carp.md"), False, FabricaCentinela())
        self.assertEqual(estado, "error")
        self.assertIn("no se pudo leer el playbook", detalle)


# ---------------------------------------------------------------------------
# Reutilización: no atado a "moneda" ni a dos opciones. `campomapeable` tiene
# 27 opciones, con dos puntos en la etiqueta y comillas y barras en la descripción.
# ---------------------------------------------------------------------------
class Reutilizacion27Opciones(Base):
    def test_validaciones_offline_pasan_con_27_opciones(self):
        comp = campo_mapeable()
        self.assertEqual(len(comp["opciones"]), 27)
        cg.validar_json_componente(comp, IDENTIDAD_VALIDA)  # no debe lanzar
        cg.validar_nombres(comp, IDENTIDAD_VALIDA)
        cg.validar_rango_offline(comp, IDENTIDAD_VALIDA)

    def test_comparacion_no_marca_diferencias_con_caracteres_especiales(self):
        comp = campo_mapeable()
        self.assertEqual(cg.comparar_contra_playbook(cuerpo_choice_conforme(comp), comp, IDENTIDAD_VALIDA), [])

    def test_creacion_feliz_de_punta_a_punta_con_27_opciones(self):
        comp = campo_mapeable()
        cliente = cliente_para_crear(comp)
        estado, nombre, detalle = self.construir_con_cliente(cliente, componente_dict=comp)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(nombre, "sanic_carp_ch_campomapeable")
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(len(posts), 1)
        self.assertEqual(len(posts[0]["cuerpo"]["Options"]), 27)

    def test_otros_choices_del_diccionario_pasan_igual(self):
        for comp in (TIPO_ARREGLO, MONEDA, COMPONENTE_VALIDO):
            with self.subTest(comp["nombre"]):
                cliente = cliente_para_crear(comp)
                estado, _, detalle = self.construir_con_cliente(cliente, componente_dict=comp)
                self.assertEqual(estado, "creado", detalle)


class Uso(unittest.TestCase):
    def test_sin_ruta_es_error_de_uso(self):
        with mock.patch.object(sys, "argv", ["choice_global.py"]), mock.patch.object(sys, "stderr"), \
                mock.patch("builtins.print") as imprimir:
            codigo = cg.main()
        self.assertEqual(codigo, 1)
        self.assertIn("uso incorrecto", imprimir.call_args[0][0])


if __name__ == "__main__":
    unittest.main()
