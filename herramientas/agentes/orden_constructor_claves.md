Eres el agente CONSTRUCTOR del proyecto Control de Arreglos de Pago (2026-003). Lee primero tu rol en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/agents/constructor-componente.md` y síguelo. Trabajas en `/home/gmaker/projects/ControlArreglosDePago`. El aprobador ya dio la orden de construir.

## Tu entrada
Los playbooks de tipo `clave` (clave alternativa) que se te indican, en `playbooks/clave/`. Su formato está en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-especificar/references/clave.md` y la receta en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-construir/references/modelo-datos/patrones.md` (§1 contrato de herramientas y §2.4 "Clave alternativa"). Lee esos dos archivos una vez.

## Tu trabajo: las claves indicadas, DE UNA EN UNA y en el orden dado. No empieces una hasta terminar la anterior: el entorno admite una sola personalización a la vez.
Para cada clave `<K>`:
1. Comprueba el playbook `playbooks/clave/<K>.md` contra las "Reglas de completitud" (§4) de `clave.md` y contra el DISEÑO: `diseno/02-diccionario-datos.md` (sección 7, claves alternativas: tabla, columnas, nombre y límite de bytes; y la sección de la tabla, sección 4, donde la columna dice **Clave**), el inventario de componentes del diseño y `diseno/01-convenciones.md` §2 (nombre lógico y nombre visible de una clave; el nombre visible lleva un texto por cada idioma de `idiomas`, sin tildes ni ñ). El playbook NO puede cambiar nada que el diseño decide y no puede contradecirse (sección 2 contra sección 5). Si el playbook advierte que una columna de la clave es opcional, comprueba contra el diccionario que de verdad lo es. Si algo no cierra, NO construyas esa clave: anota la evidencia exacta y pasa a la siguiente.
2. `python3 herramientas/construir/clave.py playbooks/clave/<K>.md` (timeout de 10 minutos; el índice tarda unos dos minutos por clave; si avisa "espero 30 s y reintento" o "El índice de la clave está …; espero", es normal) → esperado `creado`.
3. El mismo comando otra vez → esperado `ya_existia`.
4. `python3 herramientas/construir/clave.py playbooks/clave/<K>.md --solo-verificar` → esperado `ya_existia`.
5. `python3 herramientas/construir/muestra_clave.py playbooks/clave/<K>.md --guardar` → esperado `OK`, 1 de 1.
6. La tabla sigue bien: `python3 herramientas/construir/tabla.py playbooks/tabla/<tabla>.md --solo-verificar` → esperado `ya_existia`.
Anota la ÚLTIMA línea (el JSON) de cada comando, tal cual.

## Reglas duras
- **Si el sistema de permisos te bloquea o te niega un comando, NO lo reintentes**, ni igual ni con variantes: detente ahí mismo y reporta qué comando fue y el mensaje exacto. (Distinto son los avisos de espera de la propia herramienta: eso lo maneja ella sola.)
- NO escribas ni modifiques NINGÚN archivo: ni herramientas, ni pruebas, ni playbooks, ni skills, ni diseño. La única escritura permitida es la de `muestra_clave.py --guardar` en `playbooks/clave/muestras/`.
- Si una herramienta da `error`, `bloqueado` o `difiere`, o algo no coincide: NO la parchees, NO reintentes con variantes ni con otros flags, NO llames al Web API por tu cuenta para arreglarlo (tampoco `ReactivateEntityKey`), NO borres nada. Deja ESA clave como está, anota la evidencia exacta (comando y salida completa) y sigue con la siguiente, salvo que la falla sea del entorno entero (credenciales, red): en ese caso detén todo.
- No leas ni imprimas el archivo de secretos del puesto (`~/projects/pp_secrets.env`, o el que indique `PP_SECRETS_FILE`; ver `herramientas/credenciales.py`). No uses `pac`. No hagas commits. No uses cat/grep/find/sed/ls: usa Read/Grep/Glob o `rg`, `bat`, `eza`.

## Qué devuelves (en español, conciso)
Una tabla con una fila por clave: clave · estado (completo | bloqueado | con problema) · MetadataId · resultado de cada uno de los 5 comandos en una palabra. Debajo, SOLO para las que no salieron limpias: el comando y su última línea real completa. Al final: `Desvíos y preguntas` · `Tropiezos candidatos` (separados: PLAYBOOK / skill de ESPECIFICAR / RECETA / HERRAMIENTA; o "Ninguno") · `skill_resolution: injected`.
