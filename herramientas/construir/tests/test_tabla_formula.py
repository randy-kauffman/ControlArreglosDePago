"""`tabla.py` y la columna `formula` (skill power-platform-especificar 0.2.0, `tabla.md` «Columna `formula`»).

La plataforma no soporta crear una fórmula por código (Learn: «We don't support defining the formulas with code»): la
herramienta NO la crea. Crea el resto de la tabla, y para la fórmula SOLO LEE: si existe, comprueba lo que la skill exige por
lectura de metadata (punto 8 de la verificación); si no existe, devuelve `bloqueado` con la instrucción del paso manual.

Dominio: la columna `sanic_antiguedad` de Conversación (diccionario, 4.2): horas desde que el último correo del cliente espera
respuesta, calculada con `Now()` sobre `sanic_sinrespuestadesde` (que tiene que ser fecha y hora de usuario local).
Ninguna prueba toca la red."""
import copy
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import tabla as tb  # noqa: E402
import test_tabla as tt  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import label, por_idioma  # noqa: E402
from test_tabla import BORRAR, aud, col, con_cambio, cuerpo_formula, rl  # noqa: E402

TABLA = "sanic_carp_tbl_zzconversacion"
EXPRESION = "DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes) / 60"


def formula(nombre="sanic_antiguedad", **kw):
    base = col(nombre, "formula", expresion=EXPRESION, resultado="decimal", precision=2, depende_de=["sanic_sinrespuestadesde"])
    base.update(kw)
    return base


CONVERSACION = {
    "tipo": "tabla",
    "nombre": TABLA,
    "displayname": por_idioma("Conversacion"),
    "displayname_plural": por_idioma("Conversaciones"),
    "descripcion": "Hilo de correos de ida y vuelta entre el área y un cliente.",
    "propiedad": "usuario",
    "notas": False,
    "actividades": False,
    "auditoria": False,
    "primaria": {"nombre": "sanic_nombre", "displayname": por_idioma("Numero"), "descripcion": "Número correlativo de la conversación.",
                 "largo": 100, "requerida": False, "autonumerico": "CONV-{SEQNUM:6}"},
    "columnas": [
        col("sanic_sinrespuestadesde", "fechahora"),
        formula(),
        col("sanic_cantidadrespuestas", "entero", minimo=0, maximo=2147483647, requerida=True),
    ],
}
POS = 1  # índice de la fórmula en CONVERSACION["columnas"]
SIN_FORMULA = frozenset(tb.CARACTERISTICAS) - {"tipo:formula"}
TODO_MENOS_FORMULA_ENTORNO = [g for g in tt.genericas_de(CONVERSACION) if g["LogicalName"] != "sanic_antiguedad"]


def entorno_sin_formula(**kw):
    return dict(genericas=TODO_MENOS_FORMULA_ENTORNO, **kw)


Base = tt.Base


class ValidacionDeLaFormula(Base):
    CASOS = [
        # claves: las propias del tipo, exactamente
        (["columnas", POS, "expresion"], BORRAR, "expresion"),
        (["columnas", POS, "resultado"], BORRAR, "resultado"),
        (["columnas", POS, "depende_de"], BORRAR, "depende_de"),
        (["columnas", POS, "precision"], BORRAR, "precision"),                  # resultado decimal exige precision
        (["columnas", POS, "minimo"], 0, "minimo"),                            # una fórmula no lleva minimo ni maximo
        (["columnas", POS, "maximo"], 10, "maximo"),
        # resultado: solo decimal o entero
        (["columnas", POS, "resultado"], "moneda", "resultado"),
        (["columnas", POS, "resultado"], "texto", "resultado"),
        (["columnas", POS, "resultado"], "fechahora", "resultado"),
        (["columnas", POS, "resultado"], 5, "resultado"),
        (["columnas", POS, "resultado"], "entero", "precision"),               # entero no lleva precision (sobra)
        # precision de una fórmula decimal: 0 a 10
        (["columnas", POS, "precision"], 11, "precision"),
        (["columnas", POS, "precision"], -1, "precision"),
        (["columnas", POS, "precision"], "2", "precision"),
        (["columnas", POS, "precision"], True, "precision"),
        # expresion
        (["columnas", POS, "expresion"], "", "expresion"),
        (["columnas", POS, "expresion"], "   ", "expresion"),
        (["columnas", POS, "expresion"], 5, "expresion"),
        (["columnas", POS, "expresion"], "1 + " * 300, "1.000"),                # más de 1.000 caracteres
        (["columnas", POS, "expresion"], "DateDiff(sanic_sinrespuestadesde, UTCNow(), TimeUnit.Minutes) / 60", "UTCNow"),
        (["columnas", POS, "expresion"], "utcnow() + sanic_antiguedad", "UTCNow"),
        (["columnas", POS, "expresion"], "sanic_antiguedad * 2", "sí misma"),
        # depende_de
        (["columnas", POS, "depende_de"], [], "depende_de"),
        (["columnas", POS, "depende_de"], "sanic_sinrespuestadesde", "depende_de"),
        (["columnas", POS, "depende_de"], [5], "depende_de[0]"),
        (["columnas", POS, "depende_de"], ["sanic_sinrespuestadesde", "sanic_sinrespuestadesde"], "repetid"),
        (["columnas", POS, "depende_de"], ["Sanic_SinRespuestaDesde"], "depende_de[0]"),
        (["columnas", POS, "depende_de"], ["sanic_antiguedad"], "sí misma"),
        (["columnas", POS, "depende_de"], ["sanic_cantidadrespuestas"], "después"),   # ver caso propio: declarada DESPUÉS
        # una fórmula es de solo lectura: nada que exigir, proteger ni auditar
        (["columnas", POS, "requerida"], True, "requerida"),
        (["columnas", POS, "protegida"], True, "protegida"),
        (["columnas", POS, "auditoria"], True, "auditoria"),
    ]

    def test_cada_dato_invalido_es_error_nombra_la_clave_y_no_toca_la_red(self):
        for ruta, valor, esperado in self.CASOS:
            with self.subTest(f"{ruta[2:]} <- {'BORRAR' if valor is BORRAR else repr(valor)[:50]}"):
                datos = con_cambio(CONVERSACION, ruta, valor)
                if esperado == "después":
                    # la base se declara DESPUÉS de la fórmula: se la mueve al final
                    datos = copy.deepcopy(CONVERSACION)
                    datos["columnas"][POS]["depende_de"] = ["sanic_cantidadrespuestas"]
                centinela = FabricaCentinela()
                estado, _, detalle = self.correr(centinela, datos)
                self.assertEqual(estado, "error", detalle)
                self.assertIn(esperado, detalle)
                self.assertIn(f"columnas[{POS}]", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(centinela.llamada)

    def test_now_solo_con_columnas_fechahora_no_con_una_fecha_sola(self):
        """`DateDiff` no mezcla una columna de usuario local con una de solo fecha (Learn; en el spike la API respondió 405)."""
        datos = copy.deepcopy(CONVERSACION)
        datos["columnas"][0] = col("sanic_sinrespuestadesde", "fecha")  # solo fecha
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, datos)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("Now()", detalle)
        self.assertIn("fechahora", detalle)
        self.assertIn("sanic_sinrespuestadesde", detalle)
        self.assertFalse(centinela.llamada)

    def test_sin_now_una_fecha_sola_es_valida(self):
        datos = copy.deepcopy(CONVERSACION)
        datos["columnas"][0] = col("sanic_sinrespuestadesde", "fecha")
        datos["columnas"][POS]["expresion"] = "DateDiff(sanic_sinrespuestadesde, sanic_sinrespuestadesde, TimeUnit.Days)"
        cliente = tt.armar(ClienteSimulado(), datos)
        estado, _, detalle = self.con_cliente(cliente, datos, verificadas=SIN_FORMULA, permitir=True)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_ciclos_entre_formulas_se_rechazan(self):
        datos = copy.deepcopy(CONVERSACION)
        datos["columnas"] = [col("sanic_base", "entero", minimo=0, maximo=9),
                             formula("sanic_a", expresion="sanic_b + 1", resultado="entero", depende_de=["sanic_b"]),
                             formula("sanic_b", expresion="sanic_a + 1", resultado="entero", depende_de=["sanic_a"])]
        for f in datos["columnas"][1:]:
            del f["precision"]
        estado, _, detalle = self.correr(FabricaCentinela(), datos)
        self.assertEqual(estado, "error", detalle)

    def test_la_profundidad_maxima_de_la_cadena_es_10(self):
        def cadena(n):
            cols = [col("sanic_base", "entero", minimo=0, maximo=9)]
            previa = "sanic_base"
            for i in range(1, n + 1):
                nombre = f"sanic_f{i}"
                cols.append(formula(nombre, expresion=f"{previa} + 1", resultado="entero", depende_de=[previa]))
                del cols[-1]["precision"]
                previa = nombre
            return dict(copy.deepcopy(CONVERSACION), columnas=cols)

        estado, _, detalle = self.correr(FabricaCentinela(), cadena(11))
        self.assertEqual(estado, "error", detalle)
        self.assertIn("10", detalle)
        self.assertIn("profundidad", detalle)
        ok = cadena(10)
        cliente = tt.armar(ClienteSimulado(), ok)
        estado, _, detalle = self.con_cliente(cliente, ok, verificadas=SIN_FORMULA, permitir=True)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_una_dependencia_que_no_esta_en_el_playbook_se_acepta_y_se_comprueba_contra_el_entorno(self):
        """«o ya existe»: una columna base que la tabla ya tiene (por ejemplo `createdon`) no hace falta declararla."""
        datos = copy.deepcopy(CONVERSACION)
        datos["columnas"][POS]["expresion"] = "DateDiff(createdon, Now(), TimeUnit.Minutes) / 60"
        datos["columnas"][POS]["depende_de"] = ["createdon"]
        cliente = tt.armar(ClienteSimulado(), datos, por_tipo=dict(tt.filas_por_tipo(datos), DateTime=[
            {"LogicalName": "sanic_sinrespuestadesde", "Format": "DateAndTime", "DateTimeBehavior": {"Value": "UserLocal"}},
            {"LogicalName": "createdon", "Format": "DateAndTime", "DateTimeBehavior": {"Value": "UserLocal"}}]))
        estado, _, detalle = self.con_cliente(cliente, datos, verificadas=SIN_FORMULA, permitir=True)
        self.assertEqual(estado, "ya_existia", detalle)


class FormulaSinEnsayar(Base):
    def test_sin_el_flag_es_bloqueado_sin_tocar_la_red(self):
        centinela = FabricaCentinela()
        estado, _, detalle = self.correr(centinela, CONVERSACION, verificadas=SIN_FORMULA)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("tipo:formula", detalle)
        self.assertIn("--permitir-no-verificadas", detalle)
        self.assertFalse(centinela.llamada)

    def test_es_una_caracteristica_del_formato_todavia_no_verificada(self):
        self.assertIn("tipo:formula", tb.CARACTERISTICAS)
        self.assertNotIn("tipo:formula", tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertIn("tipo:formula", tb.caracteristicas_usadas(CONVERSACION))

    def test_el_ensayo_solo_vale_sobre_una_tabla_descartable(self):
        real = dict(copy.deepcopy(CONVERSACION), nombre="sanic_carp_tbl_conversacion")
        estado, _, detalle = self.correr(FabricaCentinela(), real, verificadas=SIN_FORMULA, permitir=True)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("_tbl_zz", detalle)


class PasoManual(Base):
    """Si la fórmula no existe: `bloqueado` con la instrucción, NUNCA un intento de crearla por API."""

    def kw(self, **extra):
        return dict(verificadas=SIN_FORMULA, permitir=True, **extra)

    def assertInstruccion(self, detalle):
        for pieza in ("paso manual", "sanic_antiguedad", "Power Apps", "Nueva columna", "Fórmula", EXPRESION, "precisión 2", "publicar", "sanic_sinrespuestadesde"):
            self.assertIn(pieza, detalle)

    def test_la_tabla_existe_y_falta_la_formula_es_bloqueado_con_la_instruccion_y_no_escribe(self):
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, **entorno_sin_formula())
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertInstruccion(detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_tambien(self):
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, **entorno_sin_formula())
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw(solo_verificar=True))
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertInstruccion(detalle)

    def test_la_creacion_de_la_tabla_no_manda_la_formula_y_termina_en_bloqueado_diciendo_que_la_tabla_si_se_creo(self):
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, existe=False, **entorno_sin_formula())
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("se creó", detalle)
        self.assertIn("MetadataId", detalle)
        self.assertInstruccion(detalle)
        post = next(l for l in cliente.llamadas if l["metodo"] == "POST" and l["ruta"] == "EntityDefinitions")
        enviadas = [a["SchemaName"] for a in post["cuerpo"]["Attributes"]]
        self.assertNotIn("sanic_antiguedad", enviadas)
        self.assertEqual(["sanic_nombre", "sanic_sinrespuestadesde", "sanic_cantidadrespuestas"], enviadas)
        # y NUNCA hay un intento de crearla aparte (la API la acepta, pero Microsoft no la soporta)
        for l in cliente.llamadas:
            if l["metodo"] != "GET" and isinstance(l["cuerpo"], dict):
                self.assertNotIn("sanic_antiguedad", str(l["cuerpo"].get("SchemaName", "")))
                self.assertNotIn("FormulaDefinition", str(l["cuerpo"]))

    def test_con_agregar_columnas_no_intenta_crear_la_formula(self):
        # Faltan la entera y la fórmula: --agregar-columnas agrega la entera y deja la fórmula como paso manual.
        sin = [g for g in tt.genericas_de(CONVERSACION) if g["LogicalName"] not in ("sanic_antiguedad", "sanic_cantidadrespuestas")]
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, genericas=sin)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw(agregar_columnas=True))
        # la primera lectura difiere por una columna que falta y por la fórmula pendiente: la entera se agrega, la fórmula no
        posts = [l["cuerpo"]["SchemaName"] for l in cliente.llamadas if l["metodo"] == "POST" and l["ruta"].endswith("/Attributes")]
        self.assertEqual(["sanic_cantidadrespuestas"], posts)
        self.assertNotEqual("creado", estado)

    def test_si_hay_otra_diferencia_ademas_difiere_y_la_formula_pendiente_se_nombra_tambien(self):
        pt = tt.filas_por_tipo(CONVERSACION)
        pt = con_cambio(pt, ["Integer", 0, "MaxValue"], 5)
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, por_tipo=pt, **entorno_sin_formula())
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_cantidadrespuestas.MaxValue", detalle)
        self.assertIn("sanic_antiguedad", detalle)
        self.assertFalse(cliente.hubo_escritura())


class VerificacionDeLaFormula(Base):
    def kw(self, **extra):
        return dict(verificadas=SIN_FORMULA, permitir=True, **extra)

    def test_la_formula_correcta_es_ya_existia_y_dice_que_la_expresion_no_se_comparo(self):
        cliente = tt.armar(ClienteSimulado(), CONVERSACION)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())
        self.assertIn("expresión", detalle)
        self.assertIn("export", detalle)  # la comparación fuerte es el export de la solución: pendiente de verificar

    def test_lee_con_los_select_de_la_receta_y_no_escribe_nada(self):
        cliente = tt.armar(ClienteSimulado(), CONVERSACION)
        self.con_cliente(cliente, CONVERSACION, **self.kw())
        ruta = f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='sanic_antiguedad')"
        lecturas = [l["ruta"] for l in cliente.llamadas if l["ruta"].startswith(ruta + "?")]
        self.assertEqual(1, len(lecturas))
        for propiedad in ("LogicalName", "AttributeTypeName", "SourceType", "SourceTypeMask", "FormulaDefinition", "IsValidForCreate",
                          "IsValidForUpdate", "IsSecured", "IsAuditEnabled", "RequiredLevel", "DisplayName", "Description"):
            self.assertIn(propiedad, lecturas[0])
        precision = [l["ruta"] for l in cliente.llamadas if l["ruta"].startswith(ruta + "/Microsoft.Dynamics.CRM.DecimalAttributeMetadata")]
        self.assertEqual(1, len(precision))
        self.assertIn("Precision", precision[0])

    def test_una_formula_entera_no_pide_la_precision(self):
        datos = copy.deepcopy(CONVERSACION)
        f = datos["columnas"][POS]
        f.update(resultado="entero", expresion="DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes)")
        del f["precision"]
        cliente = tt.armar(ClienteSimulado(), datos)
        estado, _, detalle = self.con_cliente(cliente, datos, **self.kw())
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual([], [l for l in cliente.llamadas if "DecimalAttributeMetadata?" in l["ruta"] and "sanic_antiguedad" in l["ruta"]])

    def test_cada_diferencia_difiere_y_nombra_la_columna_y_la_propiedad(self):
        f = CONVERSACION["columnas"][POS]
        casos = [
            ("sanic_antiguedad.SourceType", cuerpo_formula(f, SourceType=0)),
            ("sanic_antiguedad.SourceTypeMask", cuerpo_formula(f, SourceTypeMask=33)),    # bit 32: la fórmula quedó inválida
            ("sanic_antiguedad.SourceTypeMask", cuerpo_formula(f, SourceTypeMask=0)),     # falta el bit 1: fuentes del mismo registro
            ("sanic_antiguedad.IsValidForCreate", cuerpo_formula(f, IsValidForCreate=True)),
            ("sanic_antiguedad.IsValidForUpdate", cuerpo_formula(f, IsValidForUpdate=True)),
            ("sanic_antiguedad.IsSecured", cuerpo_formula(f, IsSecured=True)),
            ("sanic_antiguedad.IsAuditEnabled", cuerpo_formula(f, IsAuditEnabled=aud(True))),
            ("sanic_antiguedad.RequiredLevel", cuerpo_formula(f, RequiredLevel=rl("ApplicationRequired"))),
        ]
        for esperado, cuerpo in casos:
            with self.subTest(esperado, cuerpo=str(cuerpo)[:60]):
                cliente = tt.armar(ClienteSimulado(), CONVERSACION, formulas={"sanic_antiguedad": cuerpo})
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_el_nombre_visible_y_la_descripcion_de_la_formula_se_comprueban_en_cada_idioma(self):
        gen = tt.genericas_de(CONVERSACION)
        i = next(k for k, g in enumerate(gen) if g["LogicalName"] == "sanic_antiguedad")
        casos = [
            ("sanic_antiguedad.DisplayName[3082]", [i, "DisplayName"], label({"1033": "Antiguedad", "3082": "Otra cosa"})),
            ("sanic_antiguedad.DisplayName[3082]", [i, "DisplayName"], label("Antiguedad", idiomas=[1033])),
            ("sanic_antiguedad.Description[3082]", [i, "Description"], label("Descripción de sanic_antiguedad.", idiomas=[1033])),
            ("sanic_antiguedad.DisplayName tiene etiquetas en un idioma que el playbook no declara", [i, "DisplayName"],
             label("Antiguedad", otros_idiomas=[(1046, "Idade")])),
        ]
        for esperado, ruta, valor in casos:
            with self.subTest(esperado):
                cliente = tt.armar(ClienteSimulado(), CONVERSACION, genericas=con_cambio(gen, ruta, valor))
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_precision_distinta(self):
        cliente = ClienteSimulado()  # la regla específica va ANTES de las del camino feliz: gana la primera que coincide
        cliente.responder("GET", lambda r: "sanic_antiguedad')/Microsoft.Dynamics.CRM.DecimalAttributeMetadata" in r, (200, {"LogicalName": "sanic_antiguedad", "Precision": 4}, {}))
        tt.armar(cliente, CONVERSACION)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_antiguedad.Precision", detalle)

    def test_tipo_de_resultado_equivocado(self):
        gen = tt.genericas_de(CONVERSACION)
        i = next(k for k, g in enumerate(gen) if g["LogicalName"] == "sanic_antiguedad")
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, genericas=con_cambio(gen, [i, "AttributeTypeName"], {"Value": "IntegerType"}))
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_antiguedad.tipo", detalle)
        self.assertIn("DecimalType", detalle)

    def test_la_base_no_existe_en_el_entorno(self):
        sin_base = [g for g in tt.genericas_de(CONVERSACION) if g["LogicalName"] != "sanic_sinrespuestadesde"]
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, genericas=sin_base)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_antiguedad.depende_de", detalle)
        self.assertIn("sanic_sinrespuestadesde", detalle)

    def test_la_base_de_fecha_con_otro_comportamiento_difiere_porque_now_exige_usuario_local(self):
        pt = con_cambio(tt.filas_por_tipo(CONVERSACION), ["DateTime", 0, "DateTimeBehavior"], {"Value": "TimeZoneIndependent"})
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, por_tipo=pt)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_antiguedad.depende_de", detalle)
        self.assertIn("UserLocal", detalle)

    def test_forma_inesperada_es_error_y_nombra_la_consulta_y_el_campo(self):
        f = CONVERSACION["columnas"][POS]
        casos = [
            ("SourceType", cuerpo_formula(f, SourceType="3")),
            ("SourceType", cuerpo_formula(f, SourceType=None)),
            ("SourceTypeMask", cuerpo_formula(f, SourceTypeMask=None)),
            ("SourceTypeMask", cuerpo_formula(f, SourceTypeMask="1")),
            ("SourceTypeMask", cuerpo_formula(f, SourceTypeMask=True)),
            ("IsValidForCreate", cuerpo_formula(f, IsValidForCreate="false")),
            ("IsValidForUpdate", cuerpo_formula(f, IsValidForUpdate=None)),
            ("IsSecured", cuerpo_formula(f, IsSecured=0)),
            ("IsAuditEnabled.Value", cuerpo_formula(f, IsAuditEnabled=aud("no"))),
            ("RequiredLevel.Value", cuerpo_formula(f, RequiredLevel=rl(0))),
            ("FormulaDefinition", cuerpo_formula(f, FormulaDefinition=5)),
        ]
        for campo, cuerpo in casos:
            with self.subTest(campo, cuerpo=str(cuerpo)[:80]):
                cliente = tt.armar(ClienteSimulado(), CONVERSACION, formulas={"sanic_antiguedad": cuerpo})
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertIn("sanic_antiguedad", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_http_inesperado_al_leer_la_formula_es_error(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: r.startswith(f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='sanic_antiguedad')?"), (500, {"error": {"message": "boom"}}, {}))
        tt.armar(cliente, CONVERSACION)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "error", detalle)
        self.assertIn("HTTP 500", detalle)
        self.assertIn("sanic_antiguedad", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_corregir_nombres_no_toca_una_formula_porque_no_esta_ensayado(self):
        gen = tt.genericas_de(CONVERSACION)
        i = next(k for k, g in enumerate(gen) if g["LogicalName"] == "sanic_antiguedad")
        gen = con_cambio(gen, [i, "DisplayName"], label("Viejo"))
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, genericas=gen)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw(corregir_nombres=True))
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_antiguedad.DisplayName", detalle)
        self.assertFalse(cliente.hubo_escritura())


if __name__ == "__main__":
    unittest.main()
