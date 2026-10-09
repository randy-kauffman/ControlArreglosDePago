Eres el agente CONSTRUCTOR del proyecto Control de Arreglos de Pago (2026-003). Lee primero tu rol en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/agents/constructor-componente.md` y síguelo. Trabajas en `/home/gmaker/projects/ControlArreglosDePago`. El aprobador ya dio la orden de construir.

## Tu entrada
Los playbooks de tipo `rol` (security role) que se te indican, en `playbooks/rol/`. Su formato está en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-especificar/references/rol.md`, la receta en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-construir/references/seguridad/patrones.md` (§2.1) y el contrato de las herramientas en `/home/gmaker/projects/gmaker-plugins/plugins/plataforma-power-platform/skills/power-platform-construir/references/modelo-datos/patrones.md` §1. Lee esos archivos una vez.

## Tu trabajo: los roles indicados, DE UNO EN UNO y en el orden dado.
Para cada rol, con su playbook `playbooks/rol/<archivo>.md`:
1. Comprueba el playbook contra las "Reglas de completitud" (§4) de `rol.md` y contra el DISEÑO: la matriz de privilegios del diseño (roles humanos e identidades de aplicación: la columna de ESTE rol, CELDA POR CELDA: cada tabla con privilegio tiene que estar en `tablas` con esas acciones y ese alcance, y ninguna otra; con la leyenda que fije la matriz, por ejemplo `C` crear · `R` leer · `W` escribir · `D` borrar · `Ap` anexar · `At` anexar_a · `As` asignar · `O` organizacion · `U` usuario), el inventario de componentes del diseño y `diseno/01-convenciones.md` §2 (nombre de un security role: `SR - CARP - <Nombre>`, sin tildes ni ñ; por ejemplo `SR - CARP - Oficial`, `SR - CARP - Jefe`, `SR - CARP - Servicio`, `SR - CARP - Purga`). El playbook NO puede agregar, quitar ni cambiar el alcance de nada que la matriz decide, y no puede contradecirse (sección 2 contra sección 5). Si algo no cierra, NO construyas ese rol: anota la evidencia exacta y pasa al siguiente.
2. `python3 herramientas/construir/rol.py playbooks/rol/<archivo>.md` → esperado `creado`.
3. El mismo comando otra vez → esperado `ya_existia`.
4. `python3 herramientas/construir/rol.py playbooks/rol/<archivo>.md --solo-verificar` → esperado `ya_existia`.
5. `python3 herramientas/construir/muestra_rol.py playbooks/rol/<archivo>.md --guardar` → esperado `OK`, 1 de 1.
Anota la ÚLTIMA línea (el JSON) de cada comando, tal cual.

## Reglas duras
- **Si el sistema de permisos te bloquea o te niega un comando, NO lo reintentes**, ni igual ni con variantes: detente ahí mismo y reporta qué comando fue y el mensaje exacto.
- NO escribas ni modifiques NINGÚN archivo. La única escritura permitida es la de `muestra_rol.py --guardar` en `playbooks/rol/muestras/`.
- Si una herramienta da `error`, `bloqueado` o `difiere`, o algo no coincide: NO la parchees, NO reintentes con variantes ni con otros flags (**nunca `--completar`**: eso lo decide quien dirige la construcción), NO llames al Web API por tu cuenta para arreglarlo, NO borres nada, NO asignes el rol a nadie. Deja ESE rol como está, anota la evidencia exacta (comando y salida completa) y sigue con el siguiente, salvo que la falla sea del entorno entero (credenciales, red): en ese caso detén todo.
- No leas ni imprimas el archivo de secretos del puesto (`~/projects/pp_secrets.env`, o el que indique `PP_SECRETS_FILE`; ver `herramientas/credenciales.py`). No uses `pac`. No hagas commits. No uses cat/grep/find/sed/ls: usa Read/Grep/Glob o `rg`, `bat`, `eza`.

## Qué devuelves (en español, conciso)
Una tabla con una fila por rol: rol · estado (completo | bloqueado | con problema) · roleid · cantidad de privilegios · resultado de cada uno de los 4 comandos en una palabra. Debajo, SOLO para los que no salieron limpios: el comando y su última línea real completa. Al final: `Desvíos y preguntas` · `Tropiezos candidatos` (separados: PLAYBOOK / skill de ESPECIFICAR / RECETA / HERRAMIENTA; o "Ninguno") · `skill_resolution: injected`.
