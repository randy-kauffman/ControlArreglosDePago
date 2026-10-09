"""La guarda de `rol.py` sobre un rol que no otorga NADA.

Nueve de cada diez veces, un rol sin privilegios propios es un playbook a medio
escribir: queda un clon del rol base con nombre nuevo que no habilita nada. La
décima es legítima: un rol de IDENTIDAD, que no da acceso sino que vale por
tenerlo asignado (por ejemplo, el de la cuenta de servicio).

Por eso la excepción NO es un flag de línea de comandos, que nadie vuelve a
leer: es un campo del playbook que exige la razón POR ESCRITO. Estas pruebas
fijan ese contrato."""
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import rol as rl  # noqa: E402

from fixtures import IDENTIDAD_VALIDA as IDENTIDAD  # noqa: E402

RAZON = ("Es un rol de IDENTIDAD, no de acceso: lo que cuenta es TENER este rol "
         "asignado a la cuenta de servicio, no lo que el rol permite.")


def playbook(**extra):
    datos = {"tipo": "rol", "nombre": "SR - CARP - Servicio", "descripcion": "La cuenta de servicio.",
             "base": "App Opener", "tablas": {}, "otros_privilegios": {}}
    datos.update(extra)
    return datos


class RolSinPrivilegiosPropios(unittest.TestCase):
    def test_sin_el_campo_frena(self):
        """Sin justificación escrita, la guarda frena igual que siempre."""
        with self.assertRaises(rl.ErrorPlaybook) as e:
            rl.validar_playbook(playbook(), IDENTIDAD)
        self.assertIn("sin_privilegios_propios", str(e.exception))

    def test_con_una_razon_demasiado_corta_frena(self):
        """Un 'porque sí' no es una razón: el campo exige 40 caracteres o más."""
        with self.assertRaises(rl.ErrorPlaybook):
            rl.validar_playbook(playbook(sin_privilegios_propios="porque si"), IDENTIDAD)

    def test_en_blanco_frena(self):
        """Poner el campo vacío para saltear la guarda no cuenta como declararla."""
        with self.assertRaises(rl.ErrorPlaybook):
            rl.validar_playbook(playbook(sin_privilegios_propios="   " * 20), IDENTIDAD)

    def test_con_la_razon_escrita_pasa(self):
        """La excepción existe, y queda documentada DENTRO del artefacto."""
        rl.validar_playbook(playbook(sin_privilegios_propios=RAZON), IDENTIDAD)

    def test_el_limite_de_la_razon_son_exactamente_40_caracteres(self):
        with self.assertRaises(rl.ErrorPlaybook) as e:
            rl.validar_playbook(playbook(sin_privilegios_propios="x" * 39), IDENTIDAD)
        self.assertIn("40", str(e.exception))
        rl.validar_playbook(playbook(sin_privilegios_propios="x" * 40), IDENTIDAD)

    def test_la_razon_cuenta_sin_los_espacios_de_los_bordes(self):
        with self.assertRaises(rl.ErrorPlaybook):
            rl.validar_playbook(playbook(sin_privilegios_propios=" " * 10 + "x" * 30 + " " * 10), IDENTIDAD)

    def test_una_razon_que_no_es_texto_frena(self):
        for no_texto in (None, 12345, ["x" * 50]):
            with self.subTest(repr(no_texto)):
                with self.assertRaises(rl.ErrorPlaybook):
                    rl.validar_playbook(playbook(sin_privilegios_propios=no_texto), IDENTIDAD)

    def test_la_guarda_no_afecta_a_un_rol_normal(self):
        """Un rol que sí otorga algo no necesita el campo, y tampoco le molesta."""
        rl.validar_playbook(playbook(tablas={"sanic_carp_tbl_cuota": {"leer": "unidad"}}), IDENTIDAD)


if __name__ == "__main__":
    unittest.main()
