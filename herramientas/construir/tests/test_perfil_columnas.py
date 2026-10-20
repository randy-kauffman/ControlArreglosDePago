"""Herramienta `perfil_columnas.py`: un column security profile con sus
permisos, desde un playbook. Ninguna prueba toca la red."""
import copy
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import perfil_columnas as pc  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, SOLUTION_ID, armar_cliente_precondiciones_ok, es_ruta_solutioncomponents, es_ruta_solutions, playbook_md  # noqa: E402

BORRAR = object()
IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="perfil-columnas", inventario="6.1")
ID_PERFIL = "d" * 8 + "-0000-0000-0000-000000000004"
T_CLIENTE, T_ARREGLO = "sanic_carp_tbl_cliente", "sanic_carp_tbl_arreglo"
# El unico perfil del proyecto (diccionario de datos, seccion 8): tres columnas protegidas en dos tablas.
BASE = {"tipo": "perfil-columnas", "nombre": "CSP - CARP - Datos sensibles",
        "descripcion": "Lectura y escritura de la cédula del cliente y de la cuenta a debitar, y lectura de la cédula copiada en el arreglo.",
        "permisos": [{"tabla": T_CLIENTE, "columna": "sanic_cedula", "leer": True, "crear": True, "actualizar": True},
                     {"tabla": T_ARREGLO, "columna": "sanic_cuentadebitar", "leer": True, "crear": True, "actualizar": True},
                     {"tabla": T_ARREGLO, "columna": "sanic_instantaneacedula", "leer": True, "crear": False, "actualizar": False}]}
# Las columnas de cada tabla tal como las devuelve `Attributes`: las protegidas y una que no lo esta.
COLUMNAS_POR_TABLA = {
    T_CLIENTE: [{"LogicalName": "sanic_cedula", "IsSecured": True}, {"LogicalName": "sanic_nombre", "IsSecured": False}],
    T_ARREGLO: [{"LogicalName": "sanic_cuentadebitar", "IsSecured": True}, {"LogicalName": "sanic_instantaneacedula", "IsSecured": True},
                {"LogicalName": "sanic_numeroarreglo", "IsSecured": False}],
}
COLUMNAS = COLUMNAS_POR_TABLA[T_CLIENTE] + COLUMNAS_POR_TABLA[T_ARREGLO]


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


def fp(p, **cambios):
    si = lambda b: 4 if b else 0  # noqa: E731
    return dict({"fieldpermissionid": "fp-" + p["columna"], "entityname": p["tabla"], "attributelogicalname": p["columna"],
                 "canread": si(p["leer"]), "cancreate": si(p["crear"]), "canupdate": si(p["actualizar"]), "canreadunmasked": 0}, **cambios)


def fila_perfil(d, **cambios):
    return dict({"fieldsecurityprofileid": ID_PERFIL, "name": d["nombre"], "description": d["descripcion"], "ismanaged": False}, **cambios)


def armar(cliente, d, existe=True, perfil=None, permisos=None, componentes=None, columnas=None, tabla_existe=True):
    """Configura TODAS las rutas del camino feliz."""
    armar_cliente_precondiciones_ok(cliente, IDENT)
    st = {"existe": existe, "permisos": None if permisos is None else list(permisos)}
    def atributos(ruta, cuerpo, solucion):
        if not tabla_existe:
            return (404, {"error": "x"}, {})
        if columnas is not None:
            return (200, {"value": columnas}, {})
        tabla = ruta.split("'")[1]
        return (200, {"value": COLUMNAS_POR_TABLA[tabla]}, {})

    cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(") and "/Attributes?" in r, atributos)
    cliente.responder("GET", lambda r: r.startswith("fieldsecurityprofiles?"), lambda *_: (200, {"value": [perfil or fila_perfil(d)] if st["existe"] else []}, {}))
    cliente.responder("GET", lambda r: r.startswith("fieldpermissions?"),
                      lambda *_: (200, {"value": st["permisos"] if st["permisos"] is not None else [fp(p) for p in d["permisos"]]}, {}))
    cliente.responder("GET", es_ruta_solutioncomponents, (200, {"value": componentes if componentes is not None else [{"solutioncomponentid": "x"}]}, {}))

    def al_crear(*_):
        st["existe"] = True
        if st["permisos"] is None:
            st["permisos"] = []
        return (204, None, {})

    def al_permitir(ruta, cuerpo, solucion):
        st["permisos"].append({"fieldpermissionid": "nuevo", "entityname": cuerpo["entityname"], "attributelogicalname": cuerpo["attributelogicalname"],
                               "canread": cuerpo["canread"], "cancreate": cuerpo["cancreate"], "canupdate": cuerpo["canupdate"], "canreadunmasked": cuerpo["canreadunmasked"]})
        return (204, None, {})

    def al_corregir(ruta, cuerpo, solucion):
        if st["permisos"] is None:
            st["permisos"] = [fp(p) for p in d["permisos"]]
        id_ = ruta[len("fieldpermissions("):-1]
        for f in st["permisos"]:
            if f["fieldpermissionid"] == id_:
                f.update(cuerpo)
        return (204, None, {})

    cliente.responder("PATCH", lambda r: r.startswith("fieldpermissions("), al_corregir)
    cliente.responder("POST", "fieldsecurityprofiles", al_crear)
    cliente.responder("POST", "fieldpermissions", al_permitir)
    return cliente


class Base(unittest.TestCase):
    def correr(self, fabrica, datos, **kw):
        with tempfile.TemporaryDirectory() as dd:
            ruta = os.path.join(dd, "playbook.md")
            with open(ruta, "w", encoding="utf-8") as f:
                f.write(playbook_md(IDENT, datos))
            return pc.construir(ruta, kw.pop("solo_verificar", False), fabrica, **kw)

    def con_cliente(self, cliente, datos, **kw):
        return self.correr(lambda: cliente, datos, **kw)


class ValidacionSinRed(Base):
    CASOS = [
        (["tipo"], "rol", "tipo"),
        (["nombre"], "Datos sensibles", "CSP - CARP - "),
        (["nombre"], "CSP - OTRA - Datos sensibles", "CSP - CARP - "),
        (["nombre"], "CSP - CARP - ", "CSP - CARP - "),
        (["nombre"], "CSP - CARP - Dátos sensibles", "fuera de ASCII"),
        (["descripcion"], "", "descripcion"),
        (["permisos"], [], "permisos"),
        (["permisos"], {}, "permisos"),
        (["permisos", 0], "sanic_cedula", "permisos[0] tiene que ser un objeto"),
        (["permisos", 0, "tabla"], "cliente", "permisos[0].tabla"),
        (["permisos", 0, "columna"], "sanic_Cedula", "permisos[0].columna"),
        (["permisos", 0, "leer"], 4, "permisos[0].leer"),
        (["permisos", 2, "leer"], False, "ningún permiso"),
        (["permisos", 0, "sobra"], 1, "sobra"),
        (["permisos", 0, "crear"], BORRAR, "crear"),
        (["permisos", 2, "columna"], "sanic_cuentadebitar", "repetida"),
        (["sobra"], 1, "sobra"),
        # Un perfil es un texto unico: no lleva etiquetas por idioma (skill, `perfil-columnas.md`).
        (["displayname"], {"1033": "Datos sensibles", "3082": "Datos sensibles"}, "sobran ['displayname']"),
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


class ValidacionDeEstructura(unittest.TestCase):
    def test_el_mismo_nombre_de_columna_en_dos_tablas_distintas_no_es_repetido(self):
        datos = con_cambio(BASE, ["permisos", 2, "columna"], "sanic_cedula")
        pc.validar_playbook(datos, IDENT)  # no lanza: la clave de repeticion es (tabla, columna)

    def test_la_misma_columna_dos_veces_en_la_misma_tabla_si_lo_es(self):
        datos = con_cambio(BASE, ["permisos", 2, "columna"], "sanic_cuentadebitar")
        with self.assertRaises(pc.ErrorPlaybook) as e:
            pc.validar_playbook(datos, IDENT)
        self.assertIn(f"{T_ARREGLO}.sanic_cuentadebitar está repetida", str(e.exception))


class Precondiciones(Base):
    def bloquea(self, esperado, datos=BASE, **kw):
        cliente = armar(ClienteSimulado(), datos, existe=False, **kw)
        estado, _, detalle = self.con_cliente(cliente, datos)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn(esperado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_tabla_no_existe(self):
        self.bloquea(T_ARREGLO, tabla_existe=False)

    def test_la_columna_no_existe(self):
        self.bloquea("sanic_inexistente", datos=con_cambio(BASE, ["permisos", 0, "columna"], "sanic_inexistente"))

    def test_la_columna_existe_pero_en_otra_tabla(self):
        """`sanic_cuentadebitar` es del arreglo: pedirla sobre el cliente no se resuelve con las columnas de otra tabla."""
        self.bloquea("no existe en la tabla 'sanic_carp_tbl_cliente'", datos=con_cambio(BASE, ["permisos", 0, "columna"], "sanic_cuentadebitar"))

    def test_la_columna_no_tiene_seguridad_de_columna(self):
        """Visto el 2026-09-21: la plataforma responde 400 si la columna no está protegida."""
        self.bloquea("sanic_nombre", datos=con_cambio(BASE, ["permisos", 0, "columna"], "sanic_nombre"))

    def test_un_idioma_del_playbook_que_el_entorno_no_provisiono_bloquea_sin_escribir(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", "RetrieveProvisionedLanguages", (200, {"RetrieveProvisionedLanguages": [1033]}, {}))
        armar(cliente, BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "bloqueado", detalle)
        self.assertIn("3082", detalle)
        self.assertFalse(cliente.hubo_escritura())


class Caminos(Base):
    def test_se_crea_el_perfil_y_despues_un_permiso_por_columna(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False)
        estado, comp, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "creado", detalle)
        self.assertEqual(comp, BASE["nombre"])
        self.assertIn(ID_PERFIL, detalle)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual([p["ruta"] for p in posts], ["fieldsecurityprofiles", "fieldpermissions", "fieldpermissions", "fieldpermissions"])
        self.assertEqual([p["solucion"] for p in posts], [IDENT["solucion"]] * 4)
        self.assertEqual(posts[0]["cuerpo"], {"name": BASE["nombre"], "description": BASE["descripcion"]})
        self.assertEqual(posts[1]["cuerpo"], {"entityname": T_CLIENTE, "attributelogicalname": "sanic_cedula", "canread": 4, "cancreate": 4, "canupdate": 4,
                                              "canreadunmasked": 0, "fieldsecurityprofileid@odata.bind": f"/fieldsecurityprofiles({ID_PERFIL})"})
        self.assertEqual(posts[3]["cuerpo"], {"entityname": T_ARREGLO, "attributelogicalname": "sanic_instantaneacedula", "canread": 4, "cancreate": 0, "canupdate": 0,
                                              "canreadunmasked": 0, "fieldsecurityprofileid@odata.bind": f"/fieldsecurityprofiles({ID_PERFIL})"})

    def test_cada_permiso_del_playbook_va_a_su_propio_campo_de_la_plataforma(self):
        for campo, api in (("leer", "canread"), ("crear", "cancreate"), ("actualizar", "canupdate")):
            solo = {"tabla": T_CLIENTE, "columna": "sanic_cedula", "leer": False, "crear": False, "actualizar": False, campo: True}
            d = dict(BASE, permisos=[solo])
            with self.subTest(campo):
                cliente = armar(ClienteSimulado(), d, existe=False)
                estado, _, detalle = self.con_cliente(cliente, d)
                self.assertEqual(estado, "creado", detalle)
                cuerpo = [l["cuerpo"] for l in cliente.llamadas if l["ruta"] == "fieldpermissions"][0]
                self.assertEqual({k: cuerpo[k] for k in ("canread", "cancreate", "canupdate")}, {k: 4 if k == api else 0 for k in ("canread", "cancreate", "canupdate")})

    def test_el_nombre_con_apostrofo_se_escapa_en_la_consulta(self):
        d = dict(BASE, nombre="CSP - CARP - Datos de O'Brien")
        cliente = armar(ClienteSimulado(), d)
        self.con_cliente(cliente, d)
        self.assertTrue([l for l in cliente.llamadas if "O''Brien" in l["ruta"]])

    def test_la_pertenencia_a_la_solucion_se_busca_como_componente_de_tipo_perfil(self):
        cliente = armar(ClienteSimulado(), BASE)
        self.con_cliente(cliente, BASE)
        consultas = [l["ruta"] for l in cliente.llamadas if l["ruta"].startswith("solutioncomponents?")]
        self.assertEqual(len(consultas), 1)
        self.assertIn(f"_solutionid_value eq {SOLUTION_ID}", consultas[0])
        self.assertIn(f"objectid eq {ID_PERFIL}", consultas[0])
        self.assertIn("componenttype eq 70", consultas[0])

    def test_se_creo_pero_no_aparece_al_releer_es_error(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", "fieldsecurityprofiles", (204, None, {}))  # va antes que la regla de `armar`: no marca que el perfil ya existe
        armar(cliente, BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("no aparece al releer", detalle)
        self.assertEqual([l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"], ["fieldsecurityprofiles"])

    def test_se_creo_con_sus_permisos_pero_el_entorno_no_coincide_es_error(self):
        cliente = ClienteSimulado()
        cliente.responder("POST", "fieldpermissions", (204, None, {}))  # responde 204 pero no deja efecto
        armar(cliente, BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("se creó pero no coincide con el playbook al releer", detalle)

    def test_ya_existia_no_escribe(self):
        cliente = armar(ClienteSimulado(), BASE)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_sin_perfil_es_error_y_no_crea(self):
        cliente = armar(ClienteSimulado(), BASE, existe=False)
        estado, _, detalle = self.con_cliente(cliente, BASE, solo_verificar=True)
        self.assertEqual(estado, "error")
        self.assertIn("no existe", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_la_creacion_falla(self):
        for ruta, texto in (("fieldsecurityprofiles", "la creación falló"), ("fieldpermissions", "--completar")):
            with self.subTest(ruta):
                cliente = ClienteSimulado()
                cliente.responder("POST", ruta, (400, {"error": "boom"}, {}))
                armar(cliente, BASE, existe=False)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "error")
                self.assertIn("HTTP 400", detalle)
                self.assertIn(texto, detalle)

    def test_dos_perfiles_con_el_mismo_nombre_es_error(self):
        cliente = ClienteSimulado()
        cliente.responder("GET", lambda r: r.startswith("fieldsecurityprofiles?"), (200, {"value": [fila_perfil(BASE), fila_perfil(BASE, fieldsecurityprofileid="otro")]}, {}))
        armar(cliente, BASE)
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("2", detalle)


class Diferencias(Base):
    def test_cada_diferencia(self):
        p0, p1, p2 = BASE["permisos"]
        casos = [(f"falta el permiso sobre {T_ARREGLO}.sanic_instantaneacedula", dict(permisos=[fp(p0), fp(p1)])),
                 (f"{T_CLIENTE}.sanic_cedula.canupdate: entorno=0 playbook=4", dict(permisos=[fp(p0, canupdate=0), fp(p1), fp(p2)])),
                 (f"{T_ARREGLO}.sanic_instantaneacedula.cancreate: entorno=4 playbook=0", dict(permisos=[fp(p0), fp(p1), fp(p2, cancreate=4)])),
                 (f"{T_ARREGLO}.sanic_cuentadebitar.canreadunmasked: entorno=3 playbook=0", dict(permisos=[fp(p0), fp(p1, canreadunmasked=3), fp(p2)])),
                 (f"sobra el permiso sobre {T_CLIENTE}.sanic_otra", dict(permisos=[fp(p0), fp(p1), fp(p2), fp(dict(p0, columna="sanic_otra"))])),
                 (f"sobra el permiso sobre {T_ARREGLO}.sanic_cedula", dict(permisos=[fp(p0), fp(p1), fp(p2), fp(dict(p0, tabla=T_ARREGLO))])),
                 ("description", dict(perfil=fila_perfil(BASE, description="Otra"))),
                 ("ismanaged", dict(perfil=fila_perfil(BASE, ismanaged=True))),
                 ("pertenencia a la solución", dict(componentes=[]))]
        for esperado, kw in casos:
            with self.subTest(esperado):
                cliente = armar(ClienteSimulado(), BASE, **kw)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "difiere", detalle)
                self.assertIn(esperado, detalle)
                self.assertFalse(cliente.hubo_escritura())


class ElFiltroDeDataverseNoDistingueTildesNiMayusculas(Base):
    def test_si_el_nombre_que_vuelve_no_es_exacto_es_difiere(self):
        cliente = armar(ClienteSimulado(), BASE, perfil=fila_perfil(BASE, name="CSP - CARP - Dátos sensibles"))
        estado, _, detalle = self.con_cliente(cliente, BASE)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("name: entorno='CSP - CARP - Dátos sensibles'", detalle)
        self.assertFalse(cliente.hubo_escritura())


class Completar(Base):
    def test_agrega_solo_el_permiso_que_falta(self):
        cliente = armar(ClienteSimulado(), BASE, permisos=[fp(p) for p in BASE["permisos"][:2]])
        estado, _, detalle = self.con_cliente(cliente, BASE, completar=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn("se agregaron 1", detalle)
        self.assertIn(f"{T_ARREGLO}.sanic_instantaneacedula", detalle)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual([p["cuerpo"]["attributelogicalname"] for p in posts], ["sanic_instantaneacedula"])

    def test_no_toca_nada_si_ademas_hay_otra_diferencia_ni_con_solo_verificar(self):
        p0, p1, _ = BASE["permisos"]
        for kw_armar, kw_correr in ((dict(permisos=[fp(p0, canupdate=0), fp(p1)]), dict(completar=True)), (dict(permisos=[fp(p0), fp(p1)]), dict(completar=True, solo_verificar=True))):
            cliente = armar(ClienteSimulado(), BASE, **kw_armar)
            estado, _, detalle = self.con_cliente(cliente, BASE, **kw_correr)
            self.assertEqual(estado, "difiere", detalle)
            self.assertFalse(cliente.hubo_escritura())


class Corregir(Base):
    def patches(self, cliente):
        return [l for l in cliente.llamadas if l["metodo"] == "PATCH"]

    def test_corrige_el_permiso_que_difiere_y_relee(self):
        p0, p1, p2 = BASE["permisos"]
        cliente = armar(ClienteSimulado(), BASE, permisos=[fp(p0), fp(p1), fp(p2, canupdate=0, cancreate=0)])
        estado, _, detalle = self.con_cliente(cliente, dict(BASE, permisos=[p0, p1, dict(p2, actualizar=True)]), corregir=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertIn("se corrigieron 1", detalle)
        self.assertIn(f"{T_ARREGLO}.sanic_instantaneacedula", detalle)
        parches = self.patches(cliente)
        self.assertEqual([x["ruta"] for x in parches], ["fieldpermissions(fp-sanic_instantaneacedula)"])
        self.assertEqual(parches[0]["cuerpo"], {"canread": 4, "cancreate": 0, "canupdate": 4})
        self.assertFalse([l for l in cliente.llamadas if l["metodo"] in ("POST", "DELETE", "PUT")])

    def test_sin_la_bandera_no_corrige(self):
        p0, p1, p2 = BASE["permisos"]
        cliente = armar(ClienteSimulado(), BASE, permisos=[fp(p0, canupdate=0), fp(p1), fp(p2)])
        for kw in ({}, {"completar": True}):
            estado, _, detalle = self.con_cliente(cliente, BASE, **kw)
            self.assertEqual(estado, "difiere", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_con_solo_verificar_no_escribe(self):
        p0, p1, p2 = BASE["permisos"]
        cliente = armar(ClienteSimulado(), BASE, permisos=[fp(p0, canupdate=0), fp(p1), fp(p2)])
        estado, _, detalle = self.con_cliente(cliente, BASE, corregir=True, solo_verificar=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_no_borra_permisos_sobrantes_y_tampoco_corrige_si_hay_otra_diferencia(self):
        p0, p1, p2 = BASE["permisos"]
        sobrante = fp(dict(p0, columna="sanic_otra"))
        for perm, kw in ((dict(permisos=[fp(p0, canupdate=0), fp(p1), fp(p2), sobrante]), "sobra el permiso"),
                         (dict(permisos=[fp(p0, canupdate=0), fp(p1), fp(p2)], perfil=fila_perfil(BASE, description="Otra")), "description"),
                         (dict(permisos=[fp(p0, canupdate=0), fp(p1), fp(p2)], componentes=[]), "pertenencia"),
                         (dict(permisos=[fp(p0, canupdate=0), fp(p1), fp(p2, canreadunmasked=3)]), "canreadunmasked")):
            cliente = armar(ClienteSimulado(), BASE, **perm)
            estado, _, detalle = self.con_cliente(cliente, BASE, corregir=True)
            self.assertEqual(estado, "difiere", detalle)
            self.assertIn(kw, detalle)
            self.assertFalse(cliente.hubo_escritura())

    def test_corregir_solo_no_agrega_los_que_faltan(self):
        p0, p1, _ = BASE["permisos"]
        cliente = armar(ClienteSimulado(), BASE, permisos=[fp(p0, canupdate=0), fp(p1)])
        estado, _, detalle = self.con_cliente(cliente, BASE, corregir=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("falta el permiso", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_corregir_con_completar_corrige_y_agrega(self):
        p0, p1, _ = BASE["permisos"]
        cliente = armar(ClienteSimulado(), BASE, permisos=[fp(p0, canupdate=0), fp(p1)])
        estado, _, detalle = self.con_cliente(cliente, BASE, corregir=True, completar=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertEqual(len(self.patches(cliente)), 1)
        self.assertEqual([l["cuerpo"]["attributelogicalname"] for l in cliente.llamadas if l["metodo"] == "POST"], ["sanic_instantaneacedula"])

    def test_falla_el_patch_es_error(self):
        p0, p1, p2 = BASE["permisos"]
        cliente = ClienteSimulado()
        cliente.responder("PATCH", lambda r: r.startswith("fieldpermissions("), (400, {"error": "boom"}, {}))
        armar(cliente, BASE, permisos=[fp(p0, canupdate=0), fp(p1), fp(p2)])
        estado, _, detalle = self.con_cliente(cliente, BASE, corregir=True)
        self.assertEqual(estado, "error", detalle)
        self.assertIn("HTTP 400", detalle)

    def test_si_al_releer_sigue_distinto_no_dice_ya_existia(self):
        p0, p1, p2 = BASE["permisos"]
        cliente = ClienteSimulado()
        cliente.responder("PATCH", lambda r: r.startswith("fieldpermissions("), (204, None, {}))  # no cambia nada
        armar(cliente, BASE, permisos=[fp(p0, canupdate=0), fp(p1), fp(p2)])
        estado, _, detalle = self.con_cliente(cliente, BASE, corregir=True)
        self.assertEqual(estado, "difiere", detalle)
        self.assertIn("canupdate", detalle)

    def test_ya_existia_con_corregir_no_escribe(self):
        cliente = armar(ClienteSimulado(), BASE)
        estado, _, detalle = self.con_cliente(cliente, BASE, corregir=True)
        self.assertEqual(estado, "ya_existia", detalle)
        self.assertFalse(cliente.hubo_escritura())


class FormaYHttpInesperados(Base):
    def test_http_inesperado_es_error_y_nombra_la_consulta(self):
        for matcher, nombre in [(lambda r: r.startswith("EntityDefinitions("), "Attributes"), (lambda r: r.startswith("fieldsecurityprofiles?"), "fieldsecurityprofiles"),
                                (lambda r: r.startswith("fieldpermissions?"), "fieldpermissions"), (es_ruta_solutioncomponents, "solutioncomponents"), (es_ruta_solutions, "solutions")]:
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
        p0, p1, _ = BASE["permisos"]
        casos = [("value[0].IsSecured", dict(columnas=[con_cambio(COLUMNAS[0], ["IsSecured"], None)] + COLUMNAS[1:])),
                 ("value[0].fieldsecurityprofileid", dict(perfil=fila_perfil(BASE, fieldsecurityprofileid=None))),
                 ("value[0].ismanaged", dict(perfil=fila_perfil(BASE, ismanaged="no"))),
                 ("value[0].canread", dict(permisos=[fp(p0, canread="4"), fp(p1)])),
                 ("value[1].attributelogicalname", dict(permisos=[fp(p0), fp(p1, attributelogicalname=None)]))]
        for campo, kw in casos:
            with self.subTest(campo):
                cliente = armar(ClienteSimulado(), BASE, **kw)
                estado, _, detalle = self.con_cliente(cliente, BASE)
                self.assertEqual(estado, "error", detalle)
                self.assertIn("forma inesperada", detalle)
                self.assertIn(f"'{campo}'", detalle)
                self.assertNotIn("fallo inesperado", detalle)
                self.assertFalse(cliente.hubo_escritura())


if __name__ == "__main__":
    unittest.main()
