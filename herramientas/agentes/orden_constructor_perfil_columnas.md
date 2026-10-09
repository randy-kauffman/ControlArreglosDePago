Eres el agente CONSTRUCTOR del proyecto Control de Arreglos de Pago (2026-003). Lee primero tu rol en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/agents/constructor-componente.md` y síguelo. Trabajas en `/home/gmaker/projects/ControlArreglosDePago`. El aprobador ya dio la orden de construir.

## Tu entrada
El playbook de tipo `perfil-columnas` (column security profile) que se te indica, en `playbooks/perfil-columnas/`. Su formato está en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-especificar/references/perfil-columnas.md`, la receta en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-construir/references/seguridad/patrones.md` (§2.2) y el contrato de las herramientas en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-construir/references/modelo-datos/patrones.md` §1. Lee esos archivos una vez.

## Tu trabajo, para el perfil indicado, con su playbook `playbooks/perfil-columnas/<archivo>.md`:
1. Comprueba el playbook contra las "Reglas de completitud" (§4) de `perfil-columnas.md` y contra el DISEÑO: `diseno/02-diccionario-datos.md` sección 8 (nombre del perfil, columnas protegidas, y leer/crear/actualizar de ESE perfil), la matriz de privilegios del diseño, el inventario de componentes del diseño, `diseno/01-convenciones.md` §2 (nombre de un column security profile: sin nombre lógico, sin tildes ni ñ), la decisión de la sección 8 del diccionario (los miembros los asigna el administrador, por equipo, y NO se construyen) y el playbook de la tabla (`playbooks/tabla/<tabla>.md`: cada columna tiene que estar `protegida: true`). El playbook NO puede agregar ni quitar columnas ni cambiar un permiso que el diseño decide, y no puede contradecirse (sección 2 contra sección 5). Si algo no cierra, NO construyas: anota la evidencia exacta y detente.
2. `python3 herramientas/construir/perfil_columnas.py playbooks/perfil-columnas/<archivo>.md` → esperado `creado`.
3. El mismo comando otra vez → esperado `ya_existia`.
4. `python3 herramientas/construir/perfil_columnas.py playbooks/perfil-columnas/<archivo>.md --solo-verificar` → esperado `ya_existia`.
5. `python3 herramientas/construir/muestra_perfil_columnas.py playbooks/perfil-columnas/<archivo>.md --guardar` → esperado `OK`, 1 de 1.
6. La tabla sigue bien: `python3 herramientas/construir/tabla.py playbooks/tabla/<tabla>.md --solo-verificar` → esperado `ya_existia`.
Anota la ÚLTIMA línea (el JSON) de cada comando, tal cual.

## Reglas duras
- **Si el sistema de permisos te bloquea o te niega un comando, NO lo reintentes**, ni igual ni con variantes: detente ahí mismo y reporta qué comando fue y el mensaje exacto.
- NO escribas ni modifiques NINGÚN archivo. La única escritura permitida es la de `muestra_perfil_columnas.py --guardar` en `playbooks/perfil-columnas/muestras/`.
- Si una herramienta da `error`, `bloqueado` o `difiere`, o algo no coincide: NO la parchees, NO reintentes con variantes ni con otros flags (**nunca `--completar`**), NO llames al Web API por tu cuenta para arreglarlo, NO borres nada, **NO le agregues miembros al perfil** (ni usuarios ni equipos). Anota la evidencia exacta (comando y salida completa) y detente.
- No leas ni imprimas el archivo de secretos del puesto (`~/projects/pp_secrets.env`, o el que indique `PP_SECRETS_FILE`; ver `herramientas/credenciales.py`). No uses `pac`. No hagas commits. No uses cat/grep/find/sed/ls: usa Read/Grep/Glob o `rg`, `bat`, `eza`.

## Qué devuelves (en español, conciso)
Una tabla de una fila: perfil · estado (completo | bloqueado | con problema) · fieldsecurityprofileid · resultado de cada uno de los 5 comandos en una palabra. Debajo, SOLO si algo no salió limpio: el comando y su última línea real completa. Al final: `Desvíos y preguntas` · `Tropiezos candidatos` (separados: PLAYBOOK / skill de ESPECIFICAR / RECETA / HERRAMIENTA; o "Ninguno") · `skill_resolution: injected`.
