"""Pruebas de la ampliación de `importar_solucion.py` (brechas G-I1 a G-I3 y B3/B4 de `diseno/07`):

- botones de formulario: el contexto llega con `PrimaryControl` (Learn, "Pass data from a page as a parameter to ribbon actions":
  `PrimaryControl` es el contexto de formulario, `SelectedControl` el de grilla); en un formulario no hay filas seleccionadas;
- la tabla estándar `email` entra a la app sin exigir el prefijo del publisher;
- ocultar un botón nativo con un `DisplayRule` que siempre da falso (reversible, Learn, "Define custom actions to modify the
  ribbon") y no con `HideCustomAction` (irreversible), que queda detrás de una bandera explícita.
"""
import base64
import copy
import json
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import importar_solucion as imp  # noqa: E402
import test_importar_solucion as ti  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import armar_cliente_precondiciones_ok  # noqa: E402

IDENT, TABLA, BOTON = ti.IDENT, ti.TABLA, ti.BOTON
BASE = f"{IDENT['prefijo']}.{IDENT['abrev']}.{TABLA.split('_')[-1]}"


def boton(**cambios):
    b = copy.deepcopy(BOTON)
    b.update(cambios)
    return b


def cambio(botones, **extra):
    base = {k: v for k, v in ti.CAMBIO_RIBBON.items() if k != "ocultar_con_hidecustomaction"}
    return dict(base, botones=botones, ocultar=[], **extra)


def xml(botones, **extra):
    return imp.ribbondiffxml(IDENT, cambio(botones, **extra), [])


def comando(x, id_):
    i = x.index(f'<CommandDefinition Id="{id_}">')
    return x[i:x.index("</CommandDefinition>", i)]


FORM = boton(ubicaciones=["Form"])
MIXTO = boton(ubicaciones=["HomepageGrid", "Form"])
REGLA = "Sanic.Carp.Comandos.puedeTomar"


class BotonDeFormulario(unittest.TestCase):
    def test_el_boton_va_en_el_grupo_de_acciones_del_formulario(self):
        x = xml([FORM])
        self.assertIn(f'Location="Mscrm.Form.{TABLA}.MainTab.Actions.Controls._children"', x)
        self.assertNotIn("Mscrm.HomepageGrid.", x)

    def test_el_comando_pasa_PrimaryControl_y_nada_de_grilla(self):
        c = comando(xml([FORM]), f"{BASE}.generarformato.Command")
        self.assertIn('<CrmParameter Value="PrimaryControl" />', c)
        self.assertNotIn("SelectedControl", c)
        self.assertEqual(1, c.count("<CrmParameter"))

    def test_en_el_formulario_no_hay_regla_de_seleccion(self):
        # G-I2: en un formulario no hay filas seleccionadas; no se emite Mscrm.SelectionCountAtLeastOne.
        x = xml([FORM])
        self.assertNotIn("Mscrm.SelectionCountAtLeastOne", x)
        self.assertIn("<EnableRules />", comando(x, f"{BASE}.generarformato.Command"))

    def test_la_regla_propia_de_un_boton_de_formulario_recibe_PrimaryControl(self):
        x = xml([boton(ubicaciones=["Form"], regla=REGLA)])
        i = x.index(f'<EnableRule Id="{BASE}.generarformato.EnableRule">')
        regla = x[i:x.index("</EnableRule>", i)]
        self.assertIn(f'FunctionName="{REGLA}"', regla)
        self.assertIn('<CrmParameter Value="PrimaryControl" />', regla)
        self.assertNotIn("SelectedControl", regla)
        self.assertIn(f'<EnableRule Id="{BASE}.generarformato.EnableRule" />',
                      comando(x, f"{BASE}.generarformato.Command"))

    def test_la_regla_propia_de_un_boton_de_grilla_sigue_con_SelectedControl(self):
        x = xml([boton(regla=REGLA)])
        i = x.index(f'<EnableRule Id="{BASE}.generarformato.EnableRule">')
        regla = x[i:x.index("</EnableRule>", i)]
        self.assertIn('<CrmParameter Value="SelectedControl" />', regla)
        self.assertNotIn("PrimaryControl", regla)


class BotonEnGrillaYFormulario(unittest.TestCase):
    def setUp(self):
        self.x = xml([boton(ubicaciones=["HomepageGrid", "Form"], regla=REGLA)])

    def test_hay_un_comando_por_clase_de_contexto_con_ids_distintos(self):
        self.assertEqual(2, self.x.count("<CommandDefinition "))
        self.assertIn(f'<Button Command="{BASE}.generarformato.Grid.Command" Id="{BASE}.generarformato.HomepageGrid.Button"', self.x)
        self.assertIn(f'<Button Command="{BASE}.generarformato.Form.Command" Id="{BASE}.generarformato.Form.Button"', self.x)

    def test_el_de_grilla_conserva_lo_de_siempre(self):
        c = comando(self.x, f"{BASE}.generarformato.Grid.Command")
        self.assertIn("Mscrm.SelectionCountAtLeastOne", c)
        self.assertIn('<CrmParameter Value="SelectedControlSelectedItemIds" />', c)
        self.assertIn('<CrmParameter Value="SelectedControl" />', c)
        self.assertNotIn("PrimaryControl", c)
        self.assertIn(f'<EnableRule Id="{BASE}.generarformato.Grid.EnableRule" />', c)

    def test_el_de_formulario_solo_PrimaryControl_y_su_propia_regla(self):
        c = comando(self.x, f"{BASE}.generarformato.Form.Command")
        self.assertNotIn("Mscrm.SelectionCountAtLeastOne", c)
        self.assertIn('<CrmParameter Value="PrimaryControl" />', c)
        self.assertNotIn("SelectedControl", c)
        self.assertIn(f'<EnableRule Id="{BASE}.generarformato.Form.EnableRule" />', c)

    def test_cada_regla_propia_recibe_su_contexto(self):
        for clase, parametro, otro in (("Grid", "SelectedControl", "PrimaryControl"), ("Form", "PrimaryControl", "SelectedControl")):
            i = self.x.index(f'<EnableRule Id="{BASE}.generarformato.{clase}.EnableRule">')
            regla = self.x[i:self.x.index("</EnableRule>", i)]
            self.assertIn(f'<CrmParameter Value="{parametro}" />', regla)
            self.assertNotIn(otro, regla)

    def test_los_comandos_no_se_mezclan_con_una_sola_clase(self):
        # Un botón de una sola clase conserva el id de siempre (`<base>.Command`).
        self.assertIn(f'<CommandDefinition Id="{BASE}.generarformato.Command">', xml([BOTON]))
        self.assertNotIn(".Grid.Command", xml([BOTON]))

    def test_generar_dos_veces_da_el_mismo_texto(self):
        self.assertEqual(self.x, xml([boton(ubicaciones=["HomepageGrid", "Form"], regla=REGLA)]))


class MenuEnFormulario(unittest.TestCase):
    def test_las_opciones_de_un_menu_de_formulario_reciben_PrimaryControl(self):
        x = xml([ti._boton_menu(ubicaciones=["Form"])])
        self.assertIn(f'Location="Mscrm.Form.{TABLA}.MainTab.Actions.Controls._children"', x)
        for opcion in ("cargar", "descargar"):
            c = comando(x, f"{BASE}.plantillas.{opcion}.Command")
            self.assertIn('<CrmParameter Value="PrimaryControl" />', c)
            self.assertNotIn("SelectedControl", c)

    def test_la_regla_de_un_menu_de_formulario_recibe_PrimaryControl(self):
        x = xml([ti._boton_menu(ubicaciones=["Form"])])
        i = x.index(f'<EnableRule Id="{BASE}.plantillas.EnableRule">')
        self.assertIn('<CrmParameter Value="PrimaryControl" />', x[i:x.index("</EnableRule>", i)])

    def test_un_menu_de_grilla_sigue_con_SelectedControl(self):
        x = xml([ti._boton_menu()])
        self.assertIn('<CrmParameter Value="SelectedControl" />', comando(x, f"{BASE}.plantillas.cargar.Command"))


class ValidacionDeUbicaciones(ti.Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = imp.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def comp_con(self, b):
        return dict(copy.deepcopy(ti.COMPONENTE), cambios=[cambio([b])])

    def test_un_menu_en_grilla_y_formulario_a_la_vez_se_rechaza(self):
        self.rechaza(self.comp_con(ti._boton_menu(ubicaciones=["HomepageGrid", "Form"])), "dos botones")

    def test_un_boton_comun_en_grilla_y_formulario_se_acepta(self):
        imp.validar_playbook(json.loads(json.dumps(self.comp_con(MIXTO))), IDENT)

    def test_un_boton_solo_de_formulario_se_acepta(self):
        imp.validar_playbook(json.loads(json.dumps(self.comp_con(FORM))), IDENT)


class VerificacionDelFormulario(unittest.TestCase):
    def verificar(self, ribbon_real, botones):
        c = cambio(botones)
        original = imp.ribbon_de_la_tabla
        imp.ribbon_de_la_tabla = lambda dv, tabla: ribbon_real
        try:
            return imp._verificar_ribbon(None, c, IDENT)
        finally:
            imp.ribbon_de_la_tabla = original

    def test_un_boton_de_formulario_bien_desplegado_no_falta_nada(self):
        self.assertEqual([], self.verificar(xml([FORM]), [FORM]))

    def test_un_boton_mixto_bien_desplegado_no_falta_nada(self):
        self.assertEqual([], self.verificar(xml([MIXTO]), [MIXTO]))

    def test_un_comando_de_formulario_que_sigue_con_SelectedControl_no_se_da_por_bueno(self):
        # El defecto que motivó esto: el botón estaba y el contexto era el equivocado.
        real = xml([FORM]).replace('<CrmParameter Value="PrimaryControl" />', '<CrmParameter Value="SelectedControl" />')
        faltan = self.verificar(real, [FORM])
        self.assertTrue(any("PrimaryControl" in f for f in faltan), faltan)

    def test_falta_el_comando_del_formulario(self):
        real = xml([FORM]).replace(f'<CommandDefinition Id="{BASE}.generarformato.Command">',
                                   '<CommandDefinition Id="otro.Command">')
        faltan = self.verificar(real, [FORM])
        self.assertTrue(any("PrimaryControl" in f and "generarformato" in f for f in faltan), faltan)

    def test_la_regla_de_formulario_con_el_contexto_equivocado_no_se_da_por_buena(self):
        b = boton(ubicaciones=["Form"], regla=REGLA)
        real = xml([b])
        i = real.index(f'<EnableRule Id="{BASE}.generarformato.EnableRule">')
        fin = real.index("</EnableRule>", i)
        real = real[:i] + real[i:fin].replace("PrimaryControl", "SelectedControl") + real[fin:]
        faltan = self.verificar(real, [b])
        self.assertTrue(any("EnableRule" in f and "PrimaryControl" in f for f in faltan), faltan)

    def test_la_regla_mixta_se_verifica_por_clase(self):
        b = boton(ubicaciones=["HomepageGrid", "Form"], regla=REGLA)
        real = xml([b]).replace(f'"{BASE}.generarformato.Form.EnableRule"', '"otra.cosa"')
        faltan = self.verificar(real, [b])
        self.assertTrue(any("EnableRule" in f for f in faltan), faltan)


class TablaEstandarEmail(ti.Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = imp.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_email_entra_a_las_tablas_de_la_app_sin_el_prefijo(self):
        c = copy.deepcopy(ti.COMPONENTE)
        c["cambios"][0]["tablas"] = ti.TABLAS + ["email"]
        imp.validar_playbook(c, IDENT)

    def test_otra_tabla_estandar_sigue_rechazada(self):
        for tabla in ("account", "contact", "task", "Email", "emailserver"):
            with self.subTest(tabla):
                c = copy.deepcopy(ti.COMPONENTE)
                c["cambios"][0]["tablas"] = ti.TABLAS + [tabla]
                self.rechaza(c, "no llevan el prefijo del publisher")

    def test_el_bloque_de_componentes_trae_email_con_su_nombre_logico(self):
        nuevo, hubo = imp.reemplazar_componentes(ti.CUSTOMIZATIONS, ti.APP, ti.SITEMAP, ti.TABLAS + ["email"])
        self.assertTrue(hubo)
        self.assertIn('<AppModuleComponent type="1" schemaName="email" />', nuevo)

    def test_un_cambio_ribbon_sobre_email_sigue_exigiendo_el_prefijo(self):
        c = dict(copy.deepcopy(ti.COMPONENTE), cambios=[dict(ti.CAMBIO_RIBBON, tabla="email")])
        self.rechaza(c, "no lleva el prefijo")

    def test_email_se_puede_excluir_de_ocultar(self):
        c = dict(copy.deepcopy(ti.COMPONENTE), app=ti.APP, cambios=[ti.CAMBIO_RIBBON])
        c["tablas_excluidas_de_ocultar"] = ["email"]
        imp.validar_playbook(c, IDENT)

    def test_otra_estandar_no_se_puede_excluir(self):
        c = dict(copy.deepcopy(ti.COMPONENTE), app=ti.APP, cambios=[ti.CAMBIO_RIBBON])
        c["tablas_excluidas_de_ocultar"] = ["account"]
        self.rechaza(c, "no llevan el prefijo del publisher")

    def test_email_en_la_app_y_excluida_no_se_denuncia(self):
        cliente = ClienteSimulado()
        armar_cliente_precondiciones_ok(cliente, IDENT)
        cliente.responder("GET", lambda r: r.startswith("RetrieveEntityRibbon("),
                          (200, {"CompressedEntityXml": ti.zip_ribbon("<RibbonDefinitions></RibbonDefinitions>")}, {}))
        cliente.responder("GET", lambda r: r.startswith("appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple"),
                          (200, {"value": [{"appmoduleid": ti.APP_ID, "uniquename": ti.APP}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("RetrieveAppComponents("),
                          (200, {"value": [{"componenttype": 1, "objectid": "md-" + t} for t in (TABLA, "email")]}, {}))
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(md-"),
                          lambda r, c, s: (200, {"LogicalName": r.split("(md-")[1].split(")")[0]}, {}))
        sin_botones = {"clase": "ribbon", "tabla": TABLA, "biblioteca": "sanic_carp_wr_js_comandosarreglo",
                       "botones": [], "ocultar": [".Assign"], "ocultar_con_hidecustomaction": True}
        c = dict(copy.deepcopy(ti.COMPONENTE), app=ti.APP, cambios=[sin_botones])
        c["tablas_excluidas_de_ocultar"] = ["email"]
        estado, _, detalle = imp.construir(self.escribir(c), True, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertNotIn("AVISO", detalle)


# ---------------------------------------------------------------------------
# Ocultar un botón nativo: DisplayRule falso (reversible) o HideCustomAction (irreversible, con bandera)
# ---------------------------------------------------------------------------
CMD_GRILLA, CMD_FORM, CMD_EDIT = "Mscrm.AssignSelectedRecord", "Mscrm.AssignPrimaryRecord", "Mscrm.EditSelectedRecord"
FALSAS = '<DisplayRule Id="Mscrm.HideOnModern" /><DisplayRule Id="Mscrm.ShowOnlyOnModern" />'


def definicion(id_, display='<DisplayRule Id="Mscrm.Normal" />', acciones='<JavaScriptFunction FunctionName="x" />'):
    return (f'<CommandDefinition Id="{id_}"><EnableRules><EnableRule Id="Mscrm.E" /></EnableRules>'
            f"<DisplayRules>{display}</DisplayRules><Actions>{acciones}</Actions></CommandDefinition>")


def ribbon_nativo(anulados=(), extra=""):
    botones = (f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Assign" Command="{CMD_GRILLA}" />'
               f'<Button Id="Mscrm.Form.{TABLA}.Assign" Command="{CMD_FORM}" />'
               f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Edit" Command="{CMD_EDIT}" />')
    comandos = "".join(definicion(c, FALSAS, "") if c in anulados else definicion(c) for c in (CMD_GRILLA, CMD_FORM, CMD_EDIT))
    return f"<RibbonDefinitions>{botones}{extra}<CommandDefinitions>{comandos}</CommandDefinitions></RibbonDefinitions>"


ANULAR = {"clase": "ribbon", "tabla": TABLA, "biblioteca": "sanic_carp_wr_js_comandosarreglo", "botones": [],
          "ocultar": [".Assign"]}


class OcultarConDisplayRule(unittest.TestCase):
    def plan(self, ribbon=None, sufijos=None):
        return imp.comandos_a_anular(ribbon or ribbon_nativo(), TABLA, sufijos or [".Assign"])

    def test_los_comandos_salen_del_Command_de_cada_boton_que_coincide(self):
        self.assertEqual([CMD_FORM, CMD_GRILLA], self.plan())

    def test_un_boton_que_no_coincide_no_se_toca(self):
        self.assertNotIn(CMD_EDIT, self.plan())

    def test_la_pestana_de_graficos_no_cuenta_como_candidata(self):
        ribbon = ribbon_nativo(extra=f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Chart.Assign" Command="Mscrm.Otro" />')
        self.assertEqual([CMD_FORM, CMD_GRILLA], self.plan(ribbon))

    def test_un_comando_compartido_con_un_boton_que_no_se_oculta_bloquea(self):
        # Anular el comando apagaría también ese otro botón: no se hace en silencio.
        ribbon = ribbon_nativo(extra=f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Chart.Assign" Command="{CMD_GRILLA}" />')
        with self.assertRaises(imp.Bloqueado) as ctx:
            self.plan(ribbon)
        self.assertIn(CMD_GRILLA, str(ctx.exception))
        self.assertIn("Chart.Assign", str(ctx.exception))
        self.assertIn("ocultar_con_hidecustomaction", str(ctx.exception))

    def test_un_boton_a_ocultar_sin_Command_es_forma_inesperada(self):
        ribbon = ribbon_nativo().replace(f'Id="Mscrm.Form.{TABLA}.Assign" Command="{CMD_FORM}"', f'Id="Mscrm.Form.{TABLA}.Assign"')
        with self.assertRaises(imp.ErrorEntorno) as ctx:
            self.plan(ribbon)
        self.assertIn(f"Mscrm.Form.{TABLA}.Assign", str(ctx.exception))
        self.assertIn("Command", str(ctx.exception))

    def test_sin_botones_que_coincidan_no_hay_nada_que_anular(self):
        self.assertEqual([], self.plan(sufijos=[".Inexistente"]))

    def test_el_RibbonDiffXml_redefine_cada_comando_con_las_dos_reglas_falsas_y_sin_acciones(self):
        x = imp.ribbondiffxml(IDENT, ANULAR, [], comandos_a_anular=[CMD_FORM, CMD_GRILLA])
        for c in (CMD_FORM, CMD_GRILLA):
            d = comando(x, c)
            self.assertIn('<DisplayRule Id="Mscrm.HideOnModern" />', d)
            self.assertIn('<DisplayRule Id="Mscrm.ShowOnlyOnModern" />', d)
            self.assertIn("<Actions />", d)
        self.assertNotIn("HideCustomAction", x)
        self.assertNotIn("<CustomActions>", x)

    def test_los_comandos_anulados_van_despues_de_los_propios(self):
        c = dict(ANULAR, botones=[BOTON])
        x = imp.ribbondiffxml(IDENT, c, [], comandos_a_anular=[CMD_GRILLA])
        self.assertLess(x.index(f"{BASE}.generarformato.Command"), x.index(f'<CommandDefinition Id="{CMD_GRILLA}">'))

    def test_generar_dos_veces_da_el_mismo_texto(self):
        a = imp.ribbondiffxml(IDENT, ANULAR, [], comandos_a_anular=[CMD_FORM, CMD_GRILLA])
        self.assertEqual(a, imp.ribbondiffxml(IDENT, ANULAR, [], comandos_a_anular=[CMD_FORM, CMD_GRILLA]))

    def test_con_la_bandera_vuelve_el_HideCustomAction_y_no_se_redefine_ningun_comando(self):
        c = dict(ANULAR, ocultar_con_hidecustomaction=True)
        x = imp.ribbondiffxml(IDENT, c, [f"Mscrm.HomepageGrid.{TABLA}.Assign"], comandos_a_anular=[CMD_GRILLA])
        self.assertIn(f'<HideCustomAction HideActionId="sanic.carp.hide.', x)
        self.assertIn(f'Location="Mscrm.HomepageGrid.{TABLA}.Assign"', x)
        self.assertNotIn('<CommandDefinition Id="Mscrm.', x)

    def test_comando_anulado_exige_las_dos_reglas_dentro_del_bloque_de_ese_comando(self):
        ribbon = ribbon_nativo(anulados=[CMD_GRILLA])
        self.assertTrue(imp.comando_anulado(ribbon, CMD_GRILLA))
        self.assertFalse(imp.comando_anulado(ribbon, CMD_FORM))
        # Las reglas en el bloque de OTRO comando no cuentan: una busqueda que matchea dos cosas no verifica nada.
        solo_una = ribbon_nativo().replace(definicion(CMD_GRILLA), definicion(CMD_GRILLA, '<DisplayRule Id="Mscrm.HideOnModern" />'))
        self.assertFalse(imp.comando_anulado(solo_una, CMD_GRILLA))
        con_acciones = ribbon_nativo().replace(definicion(CMD_GRILLA), definicion(CMD_GRILLA, FALSAS))
        self.assertFalse(imp.comando_anulado(con_acciones, CMD_GRILLA))


class ValidacionDeLaBandera(ti.Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = imp.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_la_bandera_es_booleana(self):
        for valor in ("si", 1, None):
            with self.subTest(valor=valor):
                self.rechaza(dict(copy.deepcopy(ti.COMPONENTE), cambios=[dict(ANULAR, ocultar_con_hidecustomaction=valor)]),
                             "ocultar_con_hidecustomaction")

    def test_la_bandera_sin_nada_que_ocultar_se_rechaza(self):
        c = dict(ANULAR, botones=[BOTON], ocultar=[], ocultar_con_hidecustomaction=True)
        self.rechaza(dict(copy.deepcopy(ti.COMPONENTE), cambios=[c]), "no hay nada que ocultar")

    def test_la_bandera_no_vale_en_un_cambio_de_app(self):
        c = copy.deepcopy(ti.COMPONENTE)
        c["cambios"][0]["ocultar_con_hidecustomaction"] = True
        self.rechaza(c, "admite exactamente")

    def test_con_y_sin_bandera_validan(self):
        for c in (ANULAR, dict(ANULAR, ocultar_con_hidecustomaction=True), dict(ANULAR, ocultar_con_hidecustomaction=False)):
            imp.validar_playbook(dict(copy.deepcopy(ti.COMPONENTE), cambios=[c]), IDENT)


class OcultarContraElEntorno(ti.Base):
    def armar_anular(self, antes=None, despues=None, ya_hecho=False):
        cliente = ClienteSimulado()
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.importado = False
        self.antes = ribbon_nativo() if antes is None else antes
        self.despues = ribbon_nativo(anulados=[CMD_FORM, CMD_GRILLA]) if despues is None else despues
        if ya_hecho:
            self.importado = True
        cliente.responder("GET", lambda r: r.startswith("RetrieveEntityRibbon("),
                          lambda r, c, s: (200, {"CompressedEntityXml": ti.zip_ribbon(self.despues if self.importado else self.antes)}, {}))
        cliente.responder("POST", "ExportSolution",
                          (200, {"ExportSolutionFile": base64.b64encode(ti.zip_de(ti.CUST_CON_ENTIDADES)).decode("ascii")}, {}))

        def importar(ruta, cuerpo, solucion):
            self.importado = True
            self.enviado = base64.b64decode(cuerpo["CustomizationFile"])
            return (204, {}, {})

        cliente.responder("POST", "ImportSolution", importar)
        cliente.responder("GET", lambda r: r.startswith("importjobs("), (200, {"progress": 100.0, "data": ""}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        cliente.responder("GET", lambda r: r.startswith("sdkmessageprocessingsteps?"), (200, {"value": []}, {}))
        return cliente

    def correr(self, cliente, solo_verificar=False, cambio_ribbon=None):
        componente = dict(copy.deepcopy(ti.COMPONENTE), cambios=[cambio_ribbon or ANULAR])
        return imp.construir(self.escribir(componente), solo_verificar, lambda: cliente)

    def test_con_los_botones_visibles_difiere_y_no_importa(self):
        cliente = self.armar_anular()
        estado, _, detalle = self.correr(cliente, solo_verificar=True)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("siguen visibles", detalle)
        self.assertIn(CMD_GRILLA, detalle)
        self.assertNotIn("ImportSolution", [l["ruta"] for l in cliente.llamadas])

    def test_el_ciclo_importa_los_comandos_anulados_y_ningun_hide(self):
        cliente = self.armar_anular()
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("creado", estado, detalle)
        enviado = self.cust_enviado()
        self.assertIn(f'<CommandDefinition Id="{CMD_GRILLA}">', enviado)
        self.assertIn(f'<CommandDefinition Id="{CMD_FORM}">', enviado)
        self.assertNotIn("HideCustomAction", enviado)
        self.assertNotIn(CMD_EDIT, enviado)

    def test_ya_hecho_da_ya_existia_sin_importar_en_los_dos_caminos(self):
        for solo_verificar in (False, True):
            cliente = self.armar_anular(ya_hecho=True)
            estado, _, detalle = self.correr(cliente, solo_verificar)
            self.assertEqual("ya_existia", estado, detalle)
            self.assertNotIn("ImportSolution", [l["ruta"] for l in cliente.llamadas])

    def test_si_despues_de_importar_siguen_visibles_no_se_informa_exito(self):
        cliente = self.armar_anular(despues=ribbon_nativo())
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("siguen visibles", detalle)

    def test_un_comando_compartido_bloquea_antes_de_exportar(self):
        compartido = ribbon_nativo(extra=f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Chart.Assign" Command="{CMD_GRILLA}" />')
        cliente = self.armar_anular(antes=compartido)
        estado, _, detalle = self.correr(cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertNotIn("ExportSolution", [l["ruta"] for l in cliente.llamadas])

    def test_un_comando_compartido_se_puede_resolver_con_la_bandera(self):
        compartido = ribbon_nativo(extra=f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Chart.Assign" Command="{CMD_GRILLA}" />')
        despues = compartido.replace(f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Assign" Command="{CMD_GRILLA}" />', "").replace(
            f'<Button Id="Mscrm.Form.{TABLA}.Assign" Command="{CMD_FORM}" />', "")
        cliente = self.armar_anular(antes=compartido, despues=despues)
        estado, _, detalle = self.correr(cliente, cambio_ribbon=dict(ANULAR, ocultar_con_hidecustomaction=True))
        self.assertEqual("creado", estado, detalle)
        enviado = self.cust_enviado()
        self.assertIn(f'<HideCustomAction HideActionId="sanic.carp.hide.', enviado)
        self.assertNotIn(f'<CommandDefinition Id="{CMD_GRILLA}">', enviado)


if __name__ == "__main__":
    unittest.main()
