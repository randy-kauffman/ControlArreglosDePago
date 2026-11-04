# Tanda 4 de humo (2026-11-04), Dev: despliegue 3

Datos ficticios `HUMO-D43-*`, sin correos. Acciones de personas como `# User 8` (Oficial) con `MSCRMCallerID`. Las filas creadas quedan en `estado.json` (no se borró nada).

## Corrida 1 (`t10_humo_d43.py`, `t20_humo_d43_continuacion.py`; paquete 1.0.3.0)

Midió un defecto: al subir el archivo, `sanic_documentofirmado_name` todavía trae el nombre del archivo ANTERIOR. Un `.txt` pasaba (la primera subida no tenía nombre) y después ni el `.pdf` de reemplazo ni el `DELETE` de la columna se podían hacer (`CARP-ARR-026` leyendo el `.txt`): el borrador quedaba atrapado (solo se podía descartar). Cierra H-35.

## Corrida 2 (`t30_humo_d43_corrida2.py`; paquete 1.0.4.0, S-38 sin validar el tipo al subir)

18 de 18. Mensajes en español y SIN código interno en todos los rechazos:

- Número único entre productos: el mismo número como Tarjeta se rechaza al crear y al cambiar el número de un borrador (`CARP-ARR-032`).
- Formalizar sin documento (`-025`); con `.txt` (`-026`, en Formalizar); reemplazo por `.pdf` y `DELETE` sin error; Formalizar con `.pdf` pasa y deja Vigente con número 1.
- Vigente recién formalizado: reemplazar el documento se rechaza (`-027`).
- D-43: un segundo borrador del mismo crédito se crea (borradores libres), pero Formalizar se rechaza con el número del vigente (`-030`).
- Desbloquear con documento (`-028`); descartar con motivo; anular.

No cubierto: `CARP-ARR-031` (reabrir un Terminado con otro Vigente; solo pruebas de dominio) y un usuario sin el perfil de seguridad de columna.
