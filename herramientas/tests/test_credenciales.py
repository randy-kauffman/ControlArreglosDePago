"""Pruebas de `credenciales.py` y de los dos clientes hermanos (`dataverse_api.py`, `graph_api.py`).
Ninguna toca la red ni lee el archivo de secretos real: todo con archivos temporales y valores inventados."""
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from unittest import mock

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS = os.path.dirname(_AQUI)
if _HERRAMIENTAS not in sys.path:
    sys.path.insert(0, _HERRAMIENTAS)

import credenciales as cr  # noqa: E402

SECRETO = "valor-inventado-que-nunca-debe-aparecer"
CONTENIDO = f"""# comentario con texto libre
DATAVERSE_URL=https://ensayo.crm.dynamics.com/
PP_TENANT_ID=11111111-1111-1111-1111-111111111111
PP_CLIENT_ID=22222222-2222-2222-2222-222222222222
PP_CLIENT_SECRET="{SECRETO}"

export PP_MAIL_BUZON_1 = 'buzon@ejemplo.com'
LINEA_SIN_IGUAL
"""


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)

    def archivo(self, contenido=CONTENIDO, nombre="secretos.env"):
        ruta = os.path.join(self.dir.name, nombre)
        with open(ruta, "w", encoding="utf-8") as f:
            f.write(contenido)
        return ruta


class LeerSecretos(Base):
    def test_lee_pares_clave_valor_ignora_comentarios_y_quita_comillas_y_export(self):
        cfg = cr.leer_secretos(self.archivo())
        self.assertEqual(SECRETO, cfg["PP_CLIENT_SECRET"])
        self.assertEqual("https://ensayo.crm.dynamics.com/", cfg["DATAVERSE_URL"])
        self.assertEqual("buzon@ejemplo.com", cfg["PP_MAIL_BUZON_1"])
        self.assertNotIn("LINEA_SIN_IGUAL", cfg)

    def test_claves_requeridas_que_faltan_se_nombran_sin_volcar_ningun_valor(self):
        with self.assertRaises(cr.ErrorCredenciales) as ctx:
            cr.leer_secretos(self.archivo(), requeridas=("PP_CLIENT_ID", "PP_MAIL_CLIENT_ID", "PP_MAIL_TENANT_ID"))
        msg = str(ctx.exception)
        self.assertIn("PP_MAIL_CLIENT_ID", msg)
        self.assertIn("PP_MAIL_TENANT_ID", msg)
        self.assertNotIn("PP_CLIENT_ID,", msg)
        self.assertNotIn(SECRETO, msg)

    def test_una_requerida_vacia_cuenta_como_faltante(self):
        with self.assertRaises(cr.ErrorCredenciales) as ctx:
            cr.leer_secretos(self.archivo("PP_CLIENT_SECRET=\n"), requeridas=("PP_CLIENT_SECRET",))
        self.assertIn("PP_CLIENT_SECRET", str(ctx.exception))

    def test_archivo_inexistente_dice_la_ruta_y_como_indicarla(self):
        ruta = os.path.join(self.dir.name, "no_existe.env")
        with self.assertRaises(cr.ErrorCredenciales) as ctx:
            cr.leer_secretos(ruta)
        self.assertIn(ruta, str(ctx.exception))

    def test_el_contenido_del_archivo_nunca_viaja_en_un_mensaje_de_error(self):
        # Un archivo ilegible como texto: el mensaje no puede incluir lo que se alcanzó a leer.
        ruta = os.path.join(self.dir.name, "binario.env")
        with open(ruta, "wb") as f:
            f.write(b"PP_CLIENT_SECRET=" + SECRETO.encode() + b"\n\xff\xfe\x00")
        with self.assertRaises(cr.ErrorCredenciales) as ctx:
            cr.leer_secretos(ruta)
        self.assertNotIn(SECRETO, str(ctx.exception))


class RutaDeSecretos(Base):
    def config(self, rutas, variable="PP_SECRETS_FILE"):
        return {"credenciales": {"variable_entorno": variable, "rutas_por_defecto": rutas}}

    def test_la_variable_de_entorno_manda_sobre_las_rutas_por_defecto(self):
        propia = self.archivo(nombre="propia.env")
        por_defecto = self.archivo(nombre="defecto.env")
        ruta = cr.ruta_secretos(entorno={"PP_SECRETS_FILE": propia}, config=self.config([por_defecto]))
        self.assertEqual(propia, ruta)

    def test_si_la_variable_apunta_a_un_archivo_que_no_existe_es_error_y_no_cae_a_otra_ruta(self):
        por_defecto = self.archivo(nombre="defecto.env")
        with self.assertRaises(cr.ErrorCredenciales) as ctx:
            cr.ruta_secretos(entorno={"PP_SECRETS_FILE": os.path.join(self.dir.name, "nada.env")}, config=self.config([por_defecto]))
        self.assertIn("PP_SECRETS_FILE", str(ctx.exception))

    def test_sin_variable_toma_la_primera_ruta_por_defecto_que_existe_y_expande_la_tilde(self):
        existe = self.archivo(nombre="segunda.env")
        ruta = cr.ruta_secretos(entorno={}, config=self.config([os.path.join(self.dir.name, "primera.env"), existe]))
        self.assertEqual(existe, ruta)
        with mock.patch.dict(os.environ, {"HOME": self.dir.name}):
            self.archivo(nombre="en_home.env")
            self.assertEqual(os.path.join(self.dir.name, "en_home.env"),
                             cr.ruta_secretos(entorno={}, config=self.config(["~/en_home.env"])))

    def test_una_ruta_relativa_por_defecto_se_resuelve_desde_la_raiz_del_proyecto(self):
        ruta = cr.ruta_secretos(entorno={}, config=self.config(["local/pp_secrets.env"]), existe=lambda p: True)
        self.assertEqual(os.path.join(cr.RAIZ_PROYECTO, "local", "pp_secrets.env"), ruta)

    def test_ninguna_ruta_existe_es_error_que_lista_donde_buscó(self):
        with self.assertRaises(cr.ErrorCredenciales) as ctx:
            cr.ruta_secretos(entorno={}, config=self.config(["/no/existe/a.env", "/no/existe/b.env"]))
        msg = str(ctx.exception)
        self.assertIn("/no/existe/a.env", msg)
        self.assertIn("/no/existe/b.env", msg)
        self.assertIn("PP_SECRETS_FILE", msg)


class ConfiguracionDelProyecto(unittest.TestCase):
    """`proyecto.json` es el ÚNICO archivo de configuración versionado: dice dónde están las cosas, nunca los secretos
    ni los datos del proyecto (esos salen de '## 1. Identidad' de cada playbook)."""

    def setUp(self):
        with open(os.path.join(_HERRAMIENTAS, "proyecto.json"), encoding="utf-8") as f:
            self.cfg = json.load(f)

    def test_se_carga_con_la_funcion_del_modulo(self):
        self.assertEqual(self.cfg, cr.cargar_configuracion())

    def test_apunta_al_archivo_de_secretos_del_puesto_y_a_una_variable_de_entorno(self):
        self.assertIn("~/projects/pp_secrets.env", self.cfg["credenciales"]["rutas_por_defecto"])
        self.assertEqual("PP_SECRETS_FILE", self.cfg["credenciales"]["variable_entorno"])

    def test_no_contiene_nada_que_parezca_un_secreto_ni_datos_del_proyecto_que_viven_en_la_identidad(self):
        # La 'descripcion' es prosa que explica la regla; lo que se revisa son los datos.
        texto = json.dumps({k: v for k, v in self.cfg.items() if k != "descripcion"}).lower()
        for prohibido in ("secret", "token", "password", "clave_acceso", "sanic_carp_sol", "sistemas_abiertos", "15946", "3082", "1033", "publisher"):
            # 'secret' aparece solo como parte del NOMBRE de archivo 'pp_secrets.env'
            if prohibido == "secret":
                self.assertEqual(texto.count("secret"), texto.count("pp_secrets.env") + texto.count("pp_secrets_file"))
            else:
                self.assertNotIn(prohibido, texto, prohibido)


class ClientesHermanos(Base):
    """Toda corrección de un cliente es un pendiente del otro (contrato, §2.3): los dos leen las credenciales por el mismo módulo."""

    def test_ningun_cliente_trae_una_ruta_de_secretos_escrita(self):
        for nombre in ("dataverse_api.py", "graph_api.py"):
            with self.subTest(nombre):
                with open(os.path.join(_HERRAMIENTAS, nombre), encoding="utf-8") as f:
                    codigo = "\n".join(l for l in f.read().splitlines() if not l.lstrip().startswith(("#", '"""')))
                self.assertNotIn('"local", "pp_secrets.env"', codigo)
                self.assertIn("leer_secretos", codigo)

    def _token_falso(self):
        class Respuesta:
            def __enter__(self_inner):
                return self_inner

            def __exit__(self_inner, *a):
                return False

            def read(self_inner):
                return json.dumps({"access_token": "token-inventado"}).encode()

        return mock.patch("urllib.request.urlopen", return_value=Respuesta())

    def test_dataverse_lee_las_credenciales_del_archivo_indicado_y_no_imprime_nada(self):
        import dataverse_api
        salida = io.StringIO()
        with self._token_falso() as urlopen, contextlib.redirect_stdout(salida), contextlib.redirect_stderr(salida):
            dv = dataverse_api.Dataverse(archivo_env=self.archivo())
        self.assertEqual("https://ensayo.crm.dynamics.com/api/data/v9.2/", dv.api)
        self.assertEqual("", salida.getvalue())
        # el secreto viaja solo en el cuerpo de la petición de token, no en la URL
        pedido = urlopen.call_args[0][0]
        self.assertNotIn(SECRETO, pedido.full_url)

    def test_dataverse_sin_claves_dice_cuales_faltan_sin_volcar_valores(self):
        import dataverse_api
        with self.assertRaises(cr.ErrorCredenciales) as ctx:
            dataverse_api.Dataverse(archivo_env=self.archivo("DATAVERSE_URL=https://ensayo.crm.dynamics.com/\n"))
        msg = str(ctx.exception)
        self.assertIn("PP_CLIENT_SECRET", msg)
        self.assertNotIn("ensayo.crm", msg)

    def test_graph_lee_las_suyas_del_mismo_archivo(self):
        import graph_api
        contenido = CONTENIDO + "PP_MAIL_TENANT_ID=t\nPP_MAIL_CLIENT_ID=c\nPP_MAIL_CLIENT_SECRET=s\nPP_MAIL_BUZON_2=dos@ejemplo.com\n"
        with self._token_falso():
            g = graph_api.Graph(archivo_env=self.archivo(contenido))
        self.assertEqual("dos@ejemplo.com", g.buzon(2))
        with self.assertRaises(KeyError) as ctx:
            g.buzon(4)
        self.assertNotIn(SECRETO, str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
