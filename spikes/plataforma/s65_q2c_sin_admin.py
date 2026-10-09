"""Q2c (NO EJECUTADO en el Spike 2): quick find sobre spk_cedula con un principal que NO es System Administrator.

Requisitos que debe cumplir quien dirige el entorno ANTES de correrlo (no los hace este script ni el spike):
  1. Un usuario existente, sin el rol System Administrator, al que se le asigne el rol 'SPK Lector Cliente'
     (ya creado en la solucion SpikesArreglosPago, sin asignar). Se pasa su systemuserid en la variable USUARIO.
  2. Escenario B (sin perfil): el usuario NO esta en el perfil 'SPK Perfil Cedula Lectura'. Se espera: cedula oculta.
  3. Escenario A (con perfil): se agrega el usuario al perfil 'SPK Perfil Cedula Lectura' (ya creado, sin asignar). Se espera: la ve.
  4. Esperar ~30 s tras asignar rol o perfil (cache de seguridad).
Usa la cabecera MSCRMCallerID (suplantacion desde la cuenta de servicio, que es System Administrator): no inicia sesion como el usuario.

Uso:  USUARIO=<systemuserid> python3 s65_q2c_sin_admin.py
"""
import os, sys, json, urllib.parse
from dv import Dataverse
usuario = os.environ.get("USUARIO")
if not usuario:
    sys.exit("Falta USUARIO=<systemuserid>")
dv = Dataverse()
H = {"MSCRMCallerID": usuario}
est, b, _ = dv.call("GET", "WhoAmI", cabeceras=H); print("WhoAmI como suplantado:", est, b.get("UserId"))
est, b, _ = dv.call("GET", f"systemusers({usuario})?$select=fullname&$expand=systemuserroles_association($select=name)")
print("roles del suplantado:", [r["name"] for r in b.get("systemuserroles_association", [])])
est, b, _ = dv.call("GET", "spk_clientes?$select=spk_nombre,spk_cedula&$filter=startswith(spk_nombre,'Q2-')&$orderby=spk_nombre", cabeceras=H)
print("1) lectura de la columna:", est, [(r["spk_nombre"], r.get("spk_cedula")) for r in b.get("value", [])] if est == 200 else b)
def qf(term):
    fx = ("<fetch><entity name='spk_cliente'><attribute name='spk_nombre'/><filter type='and'><condition attribute='statecode' operator='eq' value='0'/></filter>"
          f"<filter type='or' isquickfindfields='1'><condition attribute='spk_nombre' operator='like' value='{term}'/>"
          f"<condition attribute='spk_cedula' operator='like' value='{term}'/></filter></entity></fetch>")
    est, b, _ = dv.call("GET", "spk_clientes?fetchXml=" + urllib.parse.quote(fx, safe=""), cabeceras=H)
    return est, [r["spk_nombre"] for r in b["value"]] if est == 200 else str(b)[:300]
print("2) quick find por cedula '001-000000-0002%':", qf("001-000000-0002%"), "(esperado en B: ninguno o error; en A: Q2-beto)")
print("3) quick find por nombre 'Q2-an%' (control):", qf("Q2-an%"), "(esperado: Q2-ana)")
