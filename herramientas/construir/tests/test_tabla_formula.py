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
FIN_DEL_PASO_MANUAL = ("después correr esta herramienta con --corregir-nombres para completar las etiquetas y la descripción en todos los idiomas "
                       "(el editor solo deja el idioma del usuario)")
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
        estado, _, detalle = self.con_cliente(cliente, datos)
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
        estado, _, detalle = self.con_cliente(cliente, ok)
        self.assertEqual(estado, "ya_existia", detalle)

    def test_una_dependencia_que_no_esta_en_el_playbook_se_acepta_y_se_comprueba_contra_el_entorno(self):
        """«o ya existe»: una columna base que la tabla ya tiene (por ejemplo `createdon`) no hace falta declararla."""
        datos = copy.deepcopy(CONVERSACION)
        datos["columnas"][POS]["expresion"] = "DateDiff(createdon, Now(), TimeUnit.Minutes) / 60"
        datos["columnas"][POS]["depende_de"] = ["createdon"]
        cliente = tt.armar(ClienteSimulado(), datos, por_tipo=dict(tt.filas_por_tipo(datos), DateTime=[
            {"LogicalName": "sanic_sinrespuestadesde", "Format": "DateAndTime", "DateTimeBehavior": {"Value": "UserLocal"}},
            {"LogicalName": "createdon", "Format": "DateAndTime", "DateTimeBehavior": {"Value": "UserLocal"}}]))
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "ya_existia", detalle)


class FormulaVerificada(Base):
    """Ensayo del 2026-10-09 (Dev, `sanic_carp_tbl_zzensayo`): una persona creó la fórmula en el editor de Power Apps y la herramienta la leyó: `FormulaDefinition`
    es EXACTAMENTE la expresión tipeada (sin `=` ni envoltorio), `SourceType` 3, `IsValidForCreate/Update` false, `RequiredLevel` None, `IsSecured` y
    `IsAuditEnabled` false. La fórmula SIGUE siendo un paso manual: la herramienta no la crea."""

    def test_es_una_caracteristica_verificada_y_ya_no_esta_sin_ensayar(self):
        self.assertIn("tipo:formula", tb.CARACTERISTICAS)
        self.assertIn("tipo:formula", tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertNotIn("tipo:formula", tb.SIN_ENSAYAR)
        self.assertNotIn("tipo:formula", tb.PENDIENTE_DE_VERIFICAR)
        self.assertIn("tipo:formula", tb.caracteristicas_usadas(CONVERSACION))
        self.assertEqual(frozenset({"decimal:precision0"}), tb.SIN_ENSAYAR)  # lo único que queda sin ensayar

    def test_una_formula_no_bloquea_por_no_verificada_ni_pide_el_flag_ni_exige_una_tabla_descartable(self):
        real = dict(copy.deepcopy(CONVERSACION), nombre="sanic_carp_tbl_conversacion")
        cliente = tt.armar(ClienteSimulado(), real)
        estado, _, detalle = self.con_cliente(cliente, real, verificadas=tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertNotIn("--permitir-no-verificadas", detalle)
        self.assertNotIn("no verificadas", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_una_formula_ausente_sigue_siendo_paso_manual_con_las_verificadas_por_defecto(self):
        real = dict(copy.deepcopy(CONVERSACION), nombre="sanic_carp_tbl_conversacion")
        sin = [g for g in tt.genericas_de(real) if g["LogicalName"] != "sanic_antiguedad"]
        cliente = tt.armar(ClienteSimulado(), real, genericas=sin)
        estado, _, detalle = self.con_cliente(cliente, real, verificadas=tb.VERIFICADAS_EN_PLATAFORMA)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("paso manual", detalle)
        self.assertFalse(cliente.hubo_escritura())


class PasoManual(Base):
    """Si la fórmula no existe: `bloqueado` con la instrucción, NUNCA un intento de crearla por API."""

    def kw(self, **extra):
        return dict(**extra)

    def assertInstruccion(self, detalle):
        for pieza in ("paso manual", "sanic_antiguedad", "Power Apps", "Nueva columna", "Fórmula", EXPRESION, "precisión 2", "publicar", "sanic_sinrespuestadesde"):
            self.assertIn(pieza, detalle)

    def test_la_instruccion_termina_pidiendo_corregir_nombres_para_completar_los_idiomas(self):
        """El editor deja el nombre visible y la descripción SOLO en el idioma del usuario: lo que sigue al paso manual es `--corregir-nombres`."""
        cliente = tt.armar(ClienteSimulado(), CONVERSACION, **entorno_sin_formula())
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertTrue(detalle.endswith(FIN_DEL_PASO_MANUAL), detalle)
        self.assertEqual(1, detalle.count("--corregir-nombres"))
        self.assertNotIn("Después se vuelve a correr", detalle)

    def test_con_dos_formulas_el_cierre_va_una_sola_vez_al_final(self):
        datos = copy.deepcopy(CONVERSACION)
        datos["columnas"].insert(POS + 1, formula("sanic_antiguedadmin", expresion="DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes)",
                                                  resultado="entero", depende_de=["sanic_sinrespuestadesde"]))
        del datos["columnas"][POS + 1]["precision"]
        sin = [g for g in tt.genericas_de(datos) if g["LogicalName"] not in ("sanic_antiguedad", "sanic_antiguedadmin")]
        cliente = tt.armar(ClienteSimulado(), datos, genericas=sin)
        estado, _, detalle = self.con_cliente(cliente, datos, **self.kw())
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("sanic_antiguedadmin", detalle)
        self.assertIn("resultado entero", detalle)
        self.assertTrue(detalle.endswith(FIN_DEL_PASO_MANUAL), detalle)
        self.assertEqual(1, detalle.count("--corregir-nombres"))

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
        return dict(**extra)

    def test_la_formula_correcta_es_ya_existia_y_no_deja_nada_sin_comparar(self):
        cliente = tt.armar(ClienteSimulado(), CONVERSACION)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())
        self.assertNotIn("Sin comparar", detalle)
        self.assertNotIn("NO se comparó", detalle)
        self.assertNotIn("pendiente de verificar", detalle)

    def armar_con_definicion(self, definicion, datos=CONVERSACION):
        f = datos["columnas"][POS]
        return tt.armar(ClienteSimulado(), datos, formulas={f["nombre"]: cuerpo_formula(f, FormulaDefinition=definicion)})

    def test_la_expresion_se_compara_con_formula_definition_normalizando_espacios(self):
        espaciadas = ["  DateDiff(sanic_sinrespuestadesde,   Now(),\tTimeUnit.Minutes)\n/  60 ", EXPRESION.replace(" ", "   "), "\n" + EXPRESION + "\n"]
        for definicion in espaciadas:
            with self.subTest(entorno=definicion):
                cliente = self.armar_con_definicion(definicion)
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
                self.assertEqual(estado, "ya_existia", detalle)
                self.assertNotIn("FormulaDefinition", detalle)
        # y al revés: el espaciado raro está en el playbook
        raro = copy.deepcopy(CONVERSACION)
        raro["columnas"][POS]["expresion"] = "DateDiff(sanic_sinrespuestadesde,   Now(),  TimeUnit.Minutes)   /    60"
        cliente = self.armar_con_definicion(EXPRESION, raro)
        estado, _, detalle = self.con_cliente(cliente, raro, **self.kw())
        self.assertEqual(estado, "ya_existia", detalle)

    def test_una_expresion_distinta_es_una_diferencia_con_las_dos_expresiones_normalizadas(self):
        casos = [
            ("otra unidad", "DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Hours) / 60", "'DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Hours) / 60'"),
            ("otro divisor", "DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes) / 24", "'DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes) / 24'"),
            ("sin espacio donde el playbook lo lleva", "DateDiff(sanic_sinrespuestadesde,Now(),TimeUnit.Minutes)/60",
             "'DateDiff(sanic_sinrespuestadesde,Now(),TimeUnit.Minutes)/60'"),
            ("el editor la guardó con un signo igual", "=" + EXPRESION, f"{('=' + EXPRESION)!r}"),
            ("vacía", "", "''"),
            ("sin definición", None, "None"),
        ]
        for nombre, definicion, mostrada in casos:
            with self.subTest(nombre):
                cliente = self.armar_con_definicion(definicion)
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(f"sanic_antiguedad.FormulaDefinition: entorno={mostrada} playbook={EXPRESION!r}", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_una_expresion_distinta_en_una_formula_entera_tambien(self):
        datos = copy.deepcopy(CONVERSACION)
        f = datos["columnas"][POS]
        f.update(resultado="entero", expresion="DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes)")
        del f["precision"]
        cliente = self.armar_con_definicion("DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Days)", datos)
        estado, _, detalle = self.con_cliente(cliente, datos, **self.kw())
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("sanic_antiguedad.FormulaDefinition: entorno='DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Days)' "
                      "playbook='DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes)'", detalle)

    def test_lee_con_los_select_de_la_receta_y_no_escribe_nada(self):
        cliente = tt.armar(ClienteSimulado(), CONVERSACION)
        self.con_cliente(cliente, CONVERSACION, **self.kw())
        ruta = f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='sanic_antiguedad')"
        # SourceTypeMask solo existe en el tipo concreto (Decimal/Integer): sin el cast la plataforma responde 400
        # "Could not find a property named 'SourceTypeMask' on type 'Microsoft.Dynamics.CRM.AttributeMetadata'" (Dev, 2026-10-09).
        self.assertEqual([], [l["ruta"] for l in cliente.llamadas if l["ruta"].startswith(ruta + "?")])
        con_cast = [l["ruta"] for l in cliente.llamadas if l["ruta"].startswith(ruta + "/Microsoft.Dynamics.CRM.DecimalAttributeMetadata?")]
        lecturas = [r for r in con_cast if "SourceTypeMask" in r]
        self.assertEqual(1, len(lecturas))
        for propiedad in ("LogicalName", "AttributeTypeName", "SourceType", "SourceTypeMask", "FormulaDefinition", "IsValidForCreate",
                          "IsValidForUpdate", "IsSecured", "IsAuditEnabled", "RequiredLevel", "DisplayName", "Description"):
            self.assertIn(propiedad, lecturas[0])
        precision = [r for r in con_cast if "Precision" in r]
        self.assertEqual(1, len(precision))

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
        cliente.responder("GET", lambda r: "sanic_antiguedad')/Microsoft.Dynamics.CRM.DecimalAttributeMetadata" in r and "SourceTypeMask" not in r,
                          (200, {"LogicalName": "sanic_antiguedad", "Precision": 4}, {}))
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
        cliente.responder("GET", lambda r: r.startswith(f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='sanic_antiguedad')/Microsoft.Dynamics.CRM.DecimalAttributeMetadata?") and "SourceTypeMask" in r, (500, {"error": {"message": "boom"}}, {}))
        tt.armar(cliente, CONVERSACION)
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **self.kw())
        self.assertEqual(estado, "error", detalle)
        self.assertIn("HTTP 500", detalle)
        self.assertIn("sanic_antiguedad", detalle)
        self.assertFalse(cliente.hubo_escritura())


RUTA_FORMULA = f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='sanic_antiguedad')"
RUTA_ENTERO = f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='sanic_cantidadrespuestas')"


def textos(etiqueta):
    return {ll["LanguageCode"]: ll["Label"] for ll in etiqueta["LocalizedLabels"]}


def genericas_del_editor(datos, nombre="sanic_antiguedad", visible=True, descripcion=True, nombre_viejo=None):
    """Las genéricas del entorno con la columna `nombre` como la deja el editor de Power Apps: nombre visible y descripción SOLO en el idioma del usuario
    (1033), y la descripción vacía. `visible` / `descripcion` eligen qué se deja así; `nombre_viejo` cambia además el texto del nombre visible."""
    gen = tt.genericas_de(datos)
    i = next(k for k, g in enumerate(gen) if g["LogicalName"] == nombre)
    c = next(c for c in datos["columnas"] if c["nombre"] == nombre)
    if visible:
        gen = con_cambio(gen, [i, "DisplayName"], label(nombre_viejo or c["displayname"]["1033"], idiomas=[1033]))
    if descripcion:
        gen = con_cambio(gen, [i, "Description"], label("", idiomas=[1033]))
    return gen


def cliente_de_correccion(datos, viejas, cast="Decimal", columnas_enteras=(), put=(204, None, {}), **armar_kw):
    """Un entorno que devuelve `viejas` hasta que alguien hace un PUT y las conformes (`genericas_de`) después. Responde la lectura completa de la fórmula
    (con el cast de su resultado) y el PUT; `columnas_enteras`: columnas enteras que también se pueden corregir; `put`: la respuesta al PUT de la fórmula."""
    cliente = ClienteSimulado()
    f = next(c for c in datos["columnas"] if c["tipo"] == "formula")
    completa = {"@odata.context": "https://x/$metadata#EntityDefinitions", "LogicalName": f["nombre"], "SchemaName": f["nombre"], "AttributeType": cast,
                "SourceType": 3, "FormulaDefinition": f["expresion"], "Precision": f.get("precision"), "DisplayName": label(f["displayname"]["1033"], idiomas=[1033]),
                "Description": label("", idiomas=[1033]), "Conservado": 7}
    cliente.responder("GET", f"{RUTA_FORMULA}/Microsoft.Dynamics.CRM.{cast}AttributeMetadata", (200, completa, {}))
    cliente.responder("PUT", RUTA_FORMULA, put)
    for n in columnas_enteras:
        ruta = f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='{n}')"
        cliente.responder("GET", f"{ruta}/Microsoft.Dynamics.CRM.IntegerAttributeMetadata",
                          (200, {"LogicalName": n, "SchemaName": n, "DisplayName": label("Viejo"), "Description": label("Vieja"), "Conservado": 9}, {}))
        cliente.responder("PUT", ruta, (204, None, {}))
    conformes = tt.genericas_de(datos)
    cliente.responder("GET", lambda r: "/Attributes?" in r,
                      lambda r, cuerpo, sol: (200, {"value": conformes if any(l["metodo"] == "PUT" for l in cliente.llamadas) else viejas}, {}))
    return tt.armar(cliente, datos, **armar_kw)


def escrituras(cliente):
    return [(l["metodo"], l["ruta"]) for l in cliente.llamadas if l["metodo"] != "GET"]


class CorregirNombresDeUnaFormula(Base):
    """`--corregir-nombres` sobre una columna de fórmula. Ensayo del 2026-10-09: el editor de Power Apps deja el nombre visible SOLO en 1033 y la descripción
    vacía (solo 1033 con ""); la reparación que funcionó es el GET de la definición completa con el cast del resultado (`DecimalAttributeMetadata`),
    `@odata.type`, `DisplayName` y `Description` en los dos idiomas, y PUT con `MSCRM.MergeLabels` y `MSCRM.SolutionUniqueName`; después PublishXml.
    `FormulaDefinition`, `SourceType` y `Precision` quedaron intactos. Solo se repara el nombre visible y la descripción de una fórmula: la descripción de
    cualquier otra columna sigue siendo una diferencia que no se toca."""

    def test_lo_que_deja_el_editor_se_completa_en_los_dos_idiomas_con_el_cast_decimal(self):
        cliente = cliente_de_correccion(CONVERSACION, genericas_del_editor(CONVERSACION))
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, corregir_nombres=True)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertIn("se corrigieron 1", detalle)
        self.assertEqual([("PUT", RUTA_FORMULA), ("POST", "PublishXml")], escrituras(cliente))
        lecturas = [l for l in cliente.llamadas if l["ruta"] == f"{RUTA_FORMULA}/Microsoft.Dynamics.CRM.DecimalAttributeMetadata"]
        self.assertEqual(1, len(lecturas))
        put = next(l for l in cliente.llamadas if l["metodo"] == "PUT")
        cuerpo = put["cuerpo"]
        self.assertEqual("Microsoft.Dynamics.CRM.DecimalAttributeMetadata", cuerpo["@odata.type"])
        self.assertEqual({1033: "Antiguedad", 3082: "Antiguedad"}, textos(cuerpo["DisplayName"]))
        self.assertEqual({1033: "Descripción de sanic_antiguedad.", 3082: "Descripción de sanic_antiguedad."}, textos(cuerpo["Description"]))
        self.assertEqual((EXPRESION, 3, 2, 7), (cuerpo["FormulaDefinition"], cuerpo["SourceType"], cuerpo["Precision"], cuerpo["Conservado"]))  # lo demás se conserva
        self.assertNotIn("@odata.context", cuerpo)
        self.assertEqual(({"MSCRM.MergeLabels": "true"}, tt.IDENT["solucion"]), (put["cabeceras"], put["solucion"]))
        # y se relee: ya no hay nada pendiente de la fórmula
        self.assertNotIn("sanic_antiguedad.DisplayName", detalle)

    def test_una_formula_de_resultado_entero_usa_el_cast_integer(self):
        datos = copy.deepcopy(CONVERSACION)
        f = datos["columnas"][POS]
        f.update(resultado="entero", expresion="DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes)")
        del f["precision"]
        cliente = cliente_de_correccion(datos, genericas_del_editor(datos), cast="Integer")
        estado, _, detalle = self.con_cliente(cliente, datos, corregir_nombres=True)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertEqual([("PUT", RUTA_FORMULA), ("POST", "PublishXml")], escrituras(cliente))
        put = next(l for l in cliente.llamadas if l["metodo"] == "PUT")
        self.assertEqual("Microsoft.Dynamics.CRM.IntegerAttributeMetadata", put["cuerpo"]["@odata.type"])
        self.assertEqual({1033: "Descripción de sanic_antiguedad.", 3082: "Descripción de sanic_antiguedad."}, textos(put["cuerpo"]["Description"]))
        self.assertEqual([], [l for l in cliente.llamadas if l["ruta"].startswith(f"{RUTA_FORMULA}/Microsoft.Dynamics.CRM.DecimalAttributeMetadata")
                              and "?" not in l["ruta"]])

    def test_una_diferencia_de_solo_la_descripcion_o_de_solo_el_nombre_de_una_formula_es_reparable(self):
        casos = [
            ("solo la descripcion", genericas_del_editor(CONVERSACION, visible=False)),
            ("solo el nombre visible", genericas_del_editor(CONVERSACION, descripcion=False)),
            ("el nombre visible con otro texto", genericas_del_editor(CONVERSACION, descripcion=False, nombre_viejo="Viejo")),
        ]
        for nombre, viejas in casos:
            with self.subTest(nombre):
                cliente = cliente_de_correccion(CONVERSACION, viejas)
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, corregir_nombres=True)
                self.assertEqual("ya_existia", estado, detalle)
                self.assertEqual([("PUT", RUTA_FORMULA), ("POST", "PublishXml")], escrituras(cliente))

    def test_el_plan_incluye_la_formula_solo_por_descripcion_y_solo_a_ella(self):
        formula_desc = "sanic_antiguedad.Description[3082]: entorno=None playbook='x'"
        self.assertEqual({"tabla": False, "columnas": ["sanic_antiguedad"], "si_no": []}, tb._nombres_que_difieren(CONVERSACION, [formula_desc]))
        # varias diferencias de la misma fórmula (nombre y descripción, por idioma) la agregan una sola vez
        varias = ["sanic_antiguedad.DisplayName[3082]: entorno=None playbook='x'", "sanic_antiguedad.Description[1033]: entorno='' playbook='x'", formula_desc]
        self.assertEqual({"tabla": False, "columnas": ["sanic_antiguedad"], "si_no": []}, tb._nombres_que_difieren(CONVERSACION, varias))
        # la descripción de CUALQUIER OTRA columna (o de la primaria) sigue sin ser reparable
        for otra in ("sanic_cantidadrespuestas", "sanic_sinrespuestadesde", "sanic_nombre"):
            with self.subTest(otra):
                self.assertIsNone(tb._nombres_que_difieren(CONVERSACION, [f"{otra}.Description[3082]: entorno=None playbook='x'"]))
                self.assertIsNone(tb._nombres_que_difieren(CONVERSACION, [formula_desc, f"{otra}.Description[3082]: entorno=None playbook='x'"]))
        self.assertIsNone(tb._nombres_que_difieren(CONVERSACION, [formula_desc, "Description[3082]: entorno=None playbook='x'"]))  # la de la tabla tampoco

    def test_junto_con_otra_columna_se_corrigen_las_dos_con_un_solo_publicar(self):
        viejas = genericas_del_editor(CONVERSACION)
        viejas = con_cambio(viejas, [next(k for k, g in enumerate(viejas) if g["LogicalName"] == "sanic_cantidadrespuestas"), "DisplayName"], label("Viejo"))
        cliente = cliente_de_correccion(CONVERSACION, viejas, columnas_enteras=("sanic_cantidadrespuestas",))
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, corregir_nombres=True)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertIn("se corrigieron 2", detalle)
        self.assertEqual([("PUT", RUTA_FORMULA), ("PUT", RUTA_ENTERO), ("POST", "PublishXml")], escrituras(cliente))
        entero = next(l for l in cliente.llamadas if l["metodo"] == "PUT" and l["ruta"] == RUTA_ENTERO)["cuerpo"]
        self.assertEqual("Microsoft.Dynamics.CRM.IntegerAttributeMetadata", entero["@odata.type"])
        # a la columna que no es fórmula NO se le toca la descripción
        self.assertEqual({1033: "Vieja", 3082: "Vieja"}, textos(entero["Description"]))

    def test_con_otra_diferencia_ademas_no_toca_nada(self):
        f = CONVERSACION["columnas"][POS]
        viejas = genericas_del_editor(CONVERSACION)
        descripcion_ajena = con_cambio(viejas, [next(k for k, g in enumerate(viejas) if g["LogicalName"] == "sanic_cantidadrespuestas"), "Description"], label("Otra"))
        casos = [
            ("la descripcion de otra columna", dict(viejas=descripcion_ajena)),
            ("la expresion de la formula", dict(viejas=viejas, formulas={"sanic_antiguedad": cuerpo_formula(f, FormulaDefinition="DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Hours)")})),
            ("el SourceType de la formula", dict(viejas=viejas, formulas={"sanic_antiguedad": cuerpo_formula(f, SourceType=0)})),
            ("la auditoria de la formula", dict(viejas=viejas, formulas={"sanic_antiguedad": cuerpo_formula(f, IsAuditEnabled=aud(True))})),
            ("una etiqueta en un idioma que el playbook no declara", dict(viejas=con_cambio(viejas, [next(k for k, g in enumerate(viejas) if g["LogicalName"] == "sanic_antiguedad"), "DisplayName"],
                                                                                        label("Antiguedad", otros_idiomas=[(1046, "Idade")]))),),
        ]
        for nombre, kw in casos:
            with self.subTest(nombre):
                viejas_caso = kw.pop("viejas")
                cliente = cliente_de_correccion(CONVERSACION, viejas_caso, **kw)
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, corregir_nombres=True)
                self.assertEqual("difiere", estado, detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_sin_el_flag_o_con_solo_verificar_difiere_y_no_escribe(self):
        for kw in ({}, dict(corregir_nombres=True, solo_verificar=True)):
            with self.subTest(kw):
                cliente = cliente_de_correccion(CONVERSACION, genericas_del_editor(CONVERSACION))
                estado, _, detalle = self.con_cliente(cliente, CONVERSACION, **kw)
                self.assertEqual("difiere", estado, detalle)
                self.assertIn("sanic_antiguedad.Description[1033]", detalle)
                self.assertIn("sanic_antiguedad.DisplayName[3082]", detalle)
                self.assertFalse(cliente.hubo_escritura())

    def test_si_el_put_de_la_formula_falla_es_error_y_dice_cual(self):
        cliente = cliente_de_correccion(CONVERSACION, genericas_del_editor(CONVERSACION), put=(400, {"error": "boom"}, {}))
        estado, _, detalle = self.con_cliente(cliente, CONVERSACION, corregir_nombres=True)
        self.assertEqual("error", estado, detalle)
        self.assertIn("HTTP 400", detalle)
        self.assertIn("sanic_antiguedad", detalle)
        self.assertIn("fórmula", detalle)
        self.assertNotIn(("POST", "PublishXml"), escrituras(cliente))


if __name__ == "__main__":
    unittest.main()
