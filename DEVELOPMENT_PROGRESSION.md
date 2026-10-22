# 📜 Registro Maestro de Progresión y Desarrollo (Forge Game)

Este documento es la **fuente de verdad persistente** sobre el avance, decisiones de diseño, estado actual del código, hitos completados y la hoja de ruta técnica del proyecto **Forge Game**. Se ubica al mismo nivel que [`DEVELOPMENT_CONSTRAINTS.md`](file:///c:/Users/mauro/forge-game/DEVELOPMENT_CONSTRAINTS.md) y debe actualizarse al inicio y al final de cada sesión de desarrollo.

---

## 🧭 1. Resumen Ejecutivo y Diagnóstico del Estado Actual

### 1.1 Estado de los Módulos Existentes
| Módulo | Estado Actual | Diagnóstico y Limitaciones |
| :--- | :--- | :--- |
| **Horno (`Furnace`)** | 🟢 **Avanzado / Funcional** | Simulación térmica, adición de carbón, compuertas de ventilación, zona dorada (+60% eficiencia), inserción de minerales y generación de lupas metálicas funcionando. |
| **Prensa (`Press`)** | 🟢 **Funcional** | Conversión de lupas en lingotes planos y rectangulares con cálculo de impurezas. |
| **Yunque (`Anvil`)** | 🟡 **Técnico pero poco divertido** | Deformación analítica de vértices, corte de polilíneas/lazos cerrados (`AnvilMeshCutter`), templado diferencial y afilado implementados. **Problema**: El martillado es estático (click o sostener click sin ritmo, timing ni juego interactivo). |
| **Ensamblaje (`Assembly`)** | 🟢 **Funcional** | Integración de hoja, pomo y mango con cálculo de estadísticas (`WeaponStats`). |
| **Combate Anterior (MH/IK)** | 🔴 **Pivote Crítico Necesario** | Se intentó replicar cinemáticas complejas de *Monster Hunter* mediante cinemática inversa procedimental (`PlayerCombatIKDriver`, `ArmIK`). Al no existir animaciones 3D gratuitas en la web con la variedad requerida, el resultado es rígido y poco orgánico. Requiere pivotar a un combate estilizado, "snappy" y fluido. |
| **Multijugador Local** | 🟡 **Parcialmente preparado** | Se diseñó soporte para `playerIndex`, viewport rects en cámaras e interacciones, pero falta el `PlayerInputManager`, separación de Canvas/UI por jugador y menú de emparejamiento de mandos. |
| **Cueva y Minería** | 🔴 **Inicial / Prototipo** | Solo existe `caveHall.fbx` y el script de menas `ResourceNodeController`. No hay nivel integrado, loop de entrada/salida, enemigos reactivos ni boss. |
| **Cofres y Almacenamiento** | 🔴 **Pendiente** | No existen cofres en el campamento para guardar recursos antes de descender a la cueva. |

---

## 🔄 2. El Pivote de Diseño: De Monster Hunter a Combate Estilizado y Fluido

### ¿Por qué pivotamos?
1. **Inviabilidad de Animaciones Gratuitas**: El sistema estilo *Monster Hunter* exige cientos de transiciones milimétricas de animación mocap por cada tipo de arma (windup, active, follow-through, cancels, hitboxes hipercomplejas).
2. **El "Valle Inquietante" del IK Puro**: Animar huesos por código analítico sin poses base de animación genera movimientos robóticos y desacoplados del suelo y del torso.
3. **El Camino a la Diversión**: Es infinitamente más divertido y satisfactorio un sistema de combate **estilizado, responsivo y ágil** (estilo *Minecraft Dungeons*, *For the King*, o juegos de acción arcade en tercera persona), donde la animación sea clara, legible, con curvas de anticipación e impacto seco (*snappy*), acompañado de un excelente "game feel" (hit-stop, screen-shake y partículas).

---

## 🎯 3. Los 5 Ejes del Plan de Desarrollo

```mermaid
graph TD
    A[Forge Game: Ejes Principales] --> B[1. Multijugador Local Co-op]
    A --> C[2. Forja Divertida: Timing y Fases]
    A --> D[3. Combate Fluido y Visualmente Coherente]
    A --> E[4. Cueva, Minería Viva y 3 Enemigos]
    A --> F[5. Cofres, Boss de Cueva y Retorno]
```

### Eje 1: Multijugador en Red Local (LAN Host-Client)
* **Objetivo**: Conexión de múltiples PCs en la misma red local (LAN) sin tope rígido de jugadores. Una PC actúa como Servidor (Host autoritativo) y las demás actúan como Clientes dependientes conectadas por IP LAN.
* **Ventajas sobre Split-Screen**:
  - Cada jugador dispone de su **pantalla completa**, su propia cámara orbital independiente y su propia UI sin solapamientos.
  - No requiere renderizar múltiples viewports simultáneos en una sola GPU.
* **Requisitos Técnicos de Red**:
  1. **Framework de Red (Netcode for GameObjects / Mirror)**:
     - Arquitectura *Server-Authoritative*: El Host simula la física del mundo, el estado térmico del horno/yunque, la salud de los enemigos y los inventarios compartidos/estaciones.
     - Los Clientes envían comandos de intención (*RPCs / ServerRpc*) y reciben variables replicadas (*NetworkVariables / SyncVars*).
  2. **Descubrimiento y Conexión LAN**:
     - Pantalla de inicio o lobby simple: "Crear Sala (Host)" / "Unirse por IP LAN" (o broadcast de descubrimiento automático en la subred).
  3. **Control de Ocupación de Estaciones en Red**:
     - Ya contemplado en [`BaseForgeStationController.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Forge/Estaciones/BaseForgeStationController.cs): Si el Cliente 1 está usando el Horno, el Servidor rechaza el acceso para los demás clientes hasta que sea liberado.
  4. **Instanciación de Jugadores**:
     - Prefab de `NetworkPlayer` que activa localmente la cámara y el input solo si `IsOwner` es verdadero.

### Eje 2: Diversión en la Forja (Especial para Ferias y Exposiciones)
* **Objetivo**: Mecánicas interactivas, táctiles y con timing visual que no dependan del sonido (ideal para entornos ruidosos de ferias).
* **Mecánicas Clave**:
  1. **Horno: Límite Térmico Natural y Fuelle Rítmico de Oxígeno**:
     - El carbón solo sube la temperatura hasta su punto de combustión natural pasiva (~700°C).
     - Al alcanzar ese techo, el botón de "Añadir Carbón" cambia automáticamente a **"Soplar Aire / Fuelle"**.
     - *Mecánica diferenciada de la prensa*: No es mantener presionado ni hacer spam. Es un **ciclo de insuflación rítmica** (el fuelle se expande llenándose de aire y se bombea en el momento exacto para inyectar una ráfaga masiva de oxígeno que aviva el fuego de golpe).
  2. **Yunque: Martillado con Timing Visual por Slider / Color**:
     - Un slider o dial con código de color de alto contraste en pantalla:
       - **Centro Dorado (Sweet Spot)**: Deformación óptima (+50%), destello visual en el metal y forja perfecta.
       - **Banda Verde**: Deformación estándar.
       - **Zonas Extremas Rojas/Grises**: Golpe a destiempo, pérdida de calor y microfisuras.
     - Feedback puramente visual con números o letreros flotantes estilo arcade (*"¡PERFECTO!"*, *"¡BUENO!"*, *"¡TARDE!"*) para garantizar comprensión en eventos ruidosos.
  3. **Templado Interactivo con Penalización por Sobre-inmersión**:
     - El jugador controla la inmersión en la batea de agua.
     - **Ventana Óptima de Enfriamiento**: Se retira la hoja cuando el siseo y el gradiente térmico alcanzan la dureza ideal.
     - **Penalización por Exceso**: Si se deja dentro del agua demasiado tiempo, el choque térmico excesivo vuelve el metal quebradizo o destemplado, **bajando la calidad del metal y reduciendo las estadísticas del arma**.

### Eje 3: Combate Adaptativo con Combos de 4 a 5 Golpes según el Arma
* **Objetivo**: Combos fluidos, realistas y diferenciados según las características físicas exactas del arma creada.
* **Variables Físicas del Arma que Modifican los Combos**:
  - **Largo / Alcance**: Un martillo con mango largo ataca con mayor inercia y barridos amplios; un martillo corto golpea con mayor cadencia vertical.
  - **Peso / Distribución de Masa**: Armas con cabeza pesada tienen anticipaciones más lentas pero mayor impacto y aturdimiento.
  - **Empuñadura (1 Mano vs 2 Manos)**: Postura a una mano permite mayor movilidad lateral; postura a dos manos compromete el torso completo en golpes devastadores.
* **Estructura del Combo de 5 Golpes**:
  1. *Golpe 1 (Apertura)*: Ataque rápido de sondeo y control de distancia.
  2. *Golpe 2 (Inercia / Retorno)*: Corte o golpe aprovechando el rebote del primero.
  3. *Golpe 3 (Ruptura)*: Golpe con peso para desestabilizar guardias.
  4. *Golpe 4 (Transición / Finta)*: Giro o preparación de máxima palanca.
  5. *Golpe 5 (Finisher / Remate)*: Impacto definitivo con efecto sísmico o hendidura profunda, acompañado de *Hit-Stop* y partículas de impacto.
* **Solución de Animación por Código / Tweens y Sockets**:
  - Poses y arcos de rotación calibrados matemáticamente por arquetipo en [`ComboCatalog.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Combat/ComboCatalog.cs) y sincronizados a los sockets de las manos ([`PlayerEquipmentController.Sockets.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Combat/PlayerEquipmentController.Sockets.cs)).

### Eje 4: Cueva y Ecosistema de Monstruos Vivos
* **Objetivo**: Una cueva subterránea viva donde los monstruos tienen su propio ciclo biológico y no existen solo para perseguir al jugador.
* **Sistema de Spawn y Anclaje**:
  - Los puntos de spawn de los monstruos son **sus nidos con crías o vetas de minerales**.
  - Los monstruos protegen este territorio frente a intrusos.
* **Ciclo de Vida y Alimentación**:
  - Los monstruos buscan minerales para masticar o cazan a otros monstruos más pequeños de la cueva para alimentarse.
* **IA por Lista de Prioridades (Tracking y Decisiones Reactivas)**:
  1. **Prioridad 1 (Autodefensa Inmediata)**: Si el monstruo recibe daño, prioriza defenderse o huir si su salud es crítica (<25%).
  2. **Prioridad 2 (Protección Territorial)**: Si un jugador se aproxima al radio de sus crías o mena, entra en agresión territorial.
  3. **Prioridad 3 (Hambre / Alimentación)**: Si no hay peligro, busca fuentes de alimento (minerales o presas) y entra en estado de ingesta.
  4. **Prioridad 4 (Patrulla / Ocio)**: Deambula calmadamente cerca de su punto de spawn.

### Eje 5: Cofres de Campamento, Boss Final y Bucle de Retorno
1. **Cofres de Campamento (Pre-Cueva)**:
   - Almacenamiento seguro antes de descender a la cueva para evitar perder minerales refinados en caso de caer en combate.
2. **Jefe de la Cueva ("El Guardián de las Profundidades")**:
   - Monstruo alfa que protege el yacimiento más rico de la cueva.
   - Combate en 2 fases con ataques telegrafiados y caída de estalactitas. Recompensa: Mineral legendario.
3. **Bucle de Retorno**:
   - Vagoneta minera o portal que transporta a los jugadores con su botín de regreso a la forja.

---

## 📅 4. Hoja de Ruta de Implementación (Roadmap Paso a Paso)

```text
Fase 1: Multijugador LAN (Host-Client)
├── 1.1 Configurar el transporte de red LAN (Host/Server y Client connection).
├── 1.2 Implementar NetworkPlayer y sincronización de movimiento/animación de avatares.
├── 1.3 Adaptar BaseForgeStationController para replicar estados de ocupación por ClientId.
└── 1.4 Crear lobby/menú LAN simple (Hostear partida / Conectar por IP LAN).

Fase 2: Gamificación de la Forja (Especial Feria)
├── 2.1 Modificar FurnaceController.Thermal para límite térmico de carbón (~700°C) y fuelle rítmico.
├── 2.2 Diseñar AnvilTimingVisualizer (slider/dial de color de alto contraste para feria).
├── 2.3 Integrar AnvilTimingController con AnvilHammerInput y multiplicadores de deformación/calidad.
└── 2.4 Hacer interactivo el templado en AnvilQuenchDifferential con degradación de calidad por sobre-inmersión.

Fase 3: Combate Adaptativo por Características de Armas
├── 3.1 Vincular WeaponStats (largo, masa, 1H/2H) al generador de combos de ComboCatalog.
├── 3.2 Implementar AttackController fluido con curvas de aceleración y WeaponTrail.
└── 3.3 Añadir Game Feel: Hit-Stop, Screen-Shake, Hit-Flash y Knockback físico proporcional a la masa del arma.

Fase 4: Nivel de Cueva, Menas y Ecosistema de Monstruos
├── 4.1 Ensamblar sector de cueva con caveHall.fbx y distribuir menas de minerales.
├── 4.2 Crear CaveEnemyAI con lista de prioridades (Autodefensa > Proteger crías/mena > Alimentarse > Patrullar).
└── 4.3 Configurar los 3 arquetipos ecológicos (Duende Minero, Escarabajo de Roca y Quiróptero Acechador).

Fase 5: Cofre de Almacenamiento, Boss de Cueva y Retorno
├── 5.1 Programar StorageChestController e interfaz para guardar/extraer ítems en el campamento.
├── 5.2 Diseñar y programar el Boss de la cueva en 2 fases con recompensas de mineral legendario.
└── 5.3 Implementar sistema de extracción (vagoneta de salida) hacia la forja.
```

---

## 📝 5. Historial de Sesiones y Registro de Progresión

### Sesión 1 (Limpieza de Restos Procedurales y Transición a Combate Fluido)
* **Objetivos Cumplidos**:
  * **Auditoría e Historial Persistente**:
    * Creación y formalización de [`DEVELOPMENT_PROGRESSION.md`](file:///c:/Users/mauro/forge-game/DEVELOPMENT_PROGRESSION.md).
    * Definición del pivote de red: Multijugador LAN Host-Client en la misma subred.
    * Especificación de forja para ferias: Indicador visual de timing (colores/slider) inmune al ruido ambiental, combustión realista de carbón con fuelle rítmico y templado con penalización por sobre-inmersión.
    * Especificación de IA de monstruos vivos: Spawn en crías/minerales, alimentación y lista de prioridades de comportamiento.
  * **Respaldo Íntegro del Sistema Procedural/Rayman**:
    * Respaldados 17 scripts del sistema procedural/IK/Rayman en la carpeta persistente `.backup_checkpoint_procedural_rayman/`.
  * **Limpieza Total del Código Activo**:
    * Eliminados todos los restos procedurales e IK que secuestraban los huesos del paladín (`PlayerCombatIKDriver`, `ArmIK`, `TorsoDriver`, `MoveSolver`, `SphericalCoords`, `WeaponComboAnimator`, `WeaponRig`, `WeaponMotionBaker`, `AttackKeyframe`, `CombatSandboxE1`, `AttackMoveValidator`, `CombatSandboxSceneSetup`).
    * Desacoplado [`PlayerEquipmentController.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Combat/PlayerEquipmentController.cs) y [`CharacterSetupTool.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/Editor/CharacterSetupTool.cs) para otorgar a Mecanim el 100% de autoridad sobre el modelo humanoide del paladín (`character.fbx`).
  * **Mapeo de Combos de 5 Golpes Distintivos (Kevin Iglesias)**:
    * Re-asignados los 5 slots de los controladores override (`Sword1H_Override`, `Axe1H_Override`, `Hammer2H_Override`, `Heavy2H_Override`) con 5 clips de animación distintos y progresivos (eliminando la repetición de `Attack2H01`).
  * **Overhaul de Gamefeel y Tactilidad**:
    * Ajustada la duración de golpe de ~0.80s-1.10s a un ritmo ágil y dinámico de ~0.36s (1H) y ~0.44s-0.50s (2H/heavy).
    * Ventana de enlace de combo a ~0.44f con buffer de entrada anticipado para transiciones inmediatas.
    * Zancada física hacia adelante (*Lunge Impulse*) en cada golpe vía `Rigidbody.linearVelocity` con impulso potenciado (+35%) en el 5° golpe finisher.
    * *Hit-Stop* táctil (0.055s normal, 0.10s finisher) y sacudida de cámara (*Screen Shake*) en impactos.
    * Retroceso físico (*Knockback*) en objetivos impactados.
  * **Validación de Compilación y Restricciones**:
    * Compilación exitosa de `Assembly-CSharp.dll` y `Assembly-CSharp-Editor.dll` con **0 errores**.
    * Cumplimiento estricto de [`DEVELOPMENT_CONSTRAINTS.md`](file:///c:/Users/mauro/forge-game/DEVELOPMENT_CONSTRAINTS.md): los 21 archivos de combate cumplen la regla de `<= 250` líneas.

---

### Sesión 2 (Corrección Integral de Conexión de Animaciones, Postura Armada y Fluidez de Combate)
* **Diagnóstico de las 4 Fallas Críticas Reportadas**:
  1. *Desconexión al correr (Brazos vacíos de civil)*: La capa 1 (`Arms` con máscara superior de Kevin Iglesias) tenía peso 0 en el Animator y su estado por defecto era `EmptyUpper`. Al correr, `LocomotionBlend` tomaba el 100% del control y los brazos se balanceaban vacíos sin empuñar el arma.
  2. *Cancelación prematura de ataques al hacer clics*: `AttackController.cs` forzaba una duración dura de `0.36s` y un enlace de combo a `0.158s`. Un clic disparaba el buffer casi de inmediato y cancelaba el golpe antes de que la espada realizara su recorrido y conectara el golpe.
  3. *Tirones y saltos de posición entre ataques*: `PlayerMovement.FixedUpdate()` sobreescribía la velocidad del Rigidbody a 0 o velocidad de caminata cada fotograma de físicas, chocando violentamente con el impulso de avance (*Lunge*) del ataque. Además, `CrossFadeInFixedTime` forzaba tiempo normalizado `0f` reiniciando la pose abruptamente.
  4. *Animaciones sin sentido con las armas*: Una espada de 1 mano tenía mapeados tajos izquierdos (`Attack1H01_L`), estocadas de lanza a dos manos (`AttackPolearm01`) y martillazos de mandoble (`Attack2H01`), y las dagas tenían asignadas animaciones de mandoble colosal.
* **Soluciones Implementadas**:
  * **Postura Marcial Superior al Correr y Caminar (Weapon Guard)**:
    * En [`PlayerAnimator.controller`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/PlayerAnimator.controller), se configuró `CombatStance` como el estado por defecto de la capa `Arms` (Upper Body) y se estableció su peso por defecto a 1.
    * En [`PlayerEquipmentController.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Combat/PlayerEquipmentController.cs), se activan las capas de torso superior (Capa 1) y agarre de mano (Capa 2) con peso `1.0f` al equipar un arma, y se desactivan a `0f` al desequipar.
    * Al atacar, la transición interna conmuta a `EmptyUpper` para otorgar autoridad completa al ataque de cuerpo entero, y al terminar el ataque regresa con un crossfade suave (0.15s) a la postura de combate.
  * **Remapeo Coherente y Realista de Animaciones por Arquetipo**:
    * **Espada 1H** ([`Sword1H_Override`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/Sword1H_Override.overrideController)): Tajo horizontal rápido (`Sword_Regular_A`), tajo diagonal reverso continuo (`Sword_Regular_B`), tajo amplio de media distancia (`Attack1H01_R`), estocada con zancada (`Sword_Dash`) y corte descendente de ejecución (`Sword_Regular_C`).
    * **Daga 1H** ([`Dagger_Override`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/Dagger_Override.overrideController)): Ataques rápidos y ágiles de filo corto sin balanceos pesados a dos manos.
    * **Hacha 1H y Maza 1H** ([`Axe1H_Override`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/Axe1H_Override.overrideController), [`Hammer1H_Override`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/Hammer1H_Override.overrideController)): Hachazo vertical contundente (`TreeChopping_Loop`), cortes horizontales y remate con aplastamiento vertical de potencia.
    * **Armas 2H Pesadas** ([`Heavy2H_Override`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/Heavy2H_Override.overrideController), [`Hammer2H_Override`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/Hammer2H_Override.overrideController)): Giros amplios de mandoble y maza colosal exclusivamente (`Attack2H01`, `NoIK@GreatswordAttack`), eliminando estocadas de lanza y golpes de escudo.
    * **Asta / Lanza** ([`Polearm_Override`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/Polearm_Override.overrideController)): Secuencias exclusivas de lanza y asta (`AttackPolearm01`, `NoIK@SpearAttack`).
  * **Timing Orgánico de Ataques y Enlace de Combos**:
    * En [`AttackController.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Combat/AttackController.cs), `attackDuration` se calcula orgánicamente desde la duración base real del movimiento (`_activeMove.baseDuration`) modulada por la velocidad del arma forjada (`attackSpeed`).
    * El encadenamiento del combo (`comboTransitionTime`) se activa **únicamente después** de que el filo del arma ha barrido la ventana activa de daño (`Mathf.Max(0.62f, _activeMove.hitWindowEnd + 0.05f)`). El jugador puede hacer clic con antelación en el buffer, pero el ataque **completa su arco completo de tajo** antes de conectar fluidamente con el siguiente.
    * Se eliminó el parámetro forzado de tiempo `0f` en `CrossFadeInFixedTime($"Attack{comboIndex + 1}", 0.09f, 0)`, permitiendo que Mecanim mezcle suavemente la pose entre el final del tajo actual y el inicio del siguiente.
  * **Física y Deslizamiento Suave (Eliminación de Tirones)**:
    * Se desacopló la física en [`PlayerMovement.Physics.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Movement/PlayerMovement.Physics.cs) (59 líneas) y [`PlayerMovement.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Movement/PlayerMovement.cs) (213 líneas).
    * Durante el ataque (`IsAttacking`), `FixedUpdate` aplica amortiguación exponencial suave sobre la velocidad horizontal (`Vector3.Lerp(currentHVel, Vector3.zero, 6.5f * Time.fixedDeltaTime)`), permitiendo que el impulso de zancada (*Lunge*) se deslice naturalmente con peso marcial estilo Monster Hunter sin cortes abruptos a cero.
  * **Validación de Compilación y Restricciones**:
    * Ambas soluciones (`Assembly-CSharp.csproj` y `Assembly-CSharp-Editor.csproj`) compilaron con **0 errores**.
    * Todos los archivos modificados cumplen estrictamente la restricción de `<= 250` líneas (`AttackController.cs`: 249, `PlayerEquipmentController.cs`: 193, `WeaponStats.cs`: 249, `PlayerMovement.cs`: 213, `PlayerMovement.Physics.cs`: 59).

---

### Sesión 3 (Ataques Plantados al Suelo, Porte Natural Cómodo y Eliminación de Sobregesticulación)
* **Diagnóstico de las Observaciones del Usuario**:
  1. *Despegue del suelo y sobregesticulación*: `Sword_Dash` (salto hacia adelante) y `Sword_Regular_C` (salto de ejecución vertical) levantaban al personaje por los aires. Además, un multiplicador de lunge de `4.8f` proyectaba al personaje a más de 2 m/s en cada tajo rápido, haciendo que los movimientos se sintieran caóticos y desproporcionados para la velocidad del ataque.
  2. *Postura forzada e incómoda al caminar*: Al fijar la capa superior de `Arms` con `CombatIdle1H01` en peso `1.0f`, el torso y los brazos quedaban congelados en pose de duelo tenso mientras las piernas caminaban, dando un aspecto rígido, ortopédico y forzado.
* **Soluciones Implementadas**:
  * **Porte Cómodo y Natural de Armas durante la Locomoción**:
    * Se configuró la capa de torso `Arms` para permanecer en peso `0f` y estado `EmptyUpper` durante la locomoción normal en [`PlayerAnimator.controller`](file:///c:/Users/mauro/forge-game/Assets/Animations/Character/PlayerAnimator.controller) y [`PlayerEquipmentController.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Combat/PlayerEquipmentController.cs).
    * La capa de manos (`Hands`, Capa 2) permanece activa con peso `1.0f` ejecutando `Human@HandsClosed01` (puños cerrados envolviendo firmemente el mango del arma).
    * **Resultado estético**: El pecho, la columna y los brazos se balancean con total cadencia orgánica y fluidez atlética al caminar y correr, con el arma sostenida de forma relajada y cómoda junto a la pierna (estilo RPG / *The Witcher* / *Monster Hunter*), eliminando toda rigidez artificial.
  * **Combos 100% Plantados al Suelo (Cero Despegue)**:
    * Se eliminaron todos los clips con saltos o desplazamientos aéreos (`Sword_Dash`, `Sword_Regular_C`, `TreeChopping_Loop`).
    * Secuencia de 5 tajos firmemente enraizados al suelo para Espada, Daga, Hacha y Maza:
      1. `Sword_Regular_A` (0.43s): Tajo horizontal rasante rápido de derecha a izquierda con pies plantados.
      2. `Sword_Regular_B` (0.53s): Tajo diagonal de retorno continuo de izquierda a derecha con pies plantados.
      3. `Attack1H01_R` (0.83s): Corte amplio marcial y pesado sin despegue de talones.
      4. `Sword_Regular_A_Rec` (0.97s): Tajo profundo y comprometido con apoyo total en el suelo.
      5. `Sword_Regular_B_Rec` (1.03s): Remate de barrido amplio con inercia controlada a nivel del piso.
  * **Contención de Impulso Físico y Anclaje Vertical**:
    * En [`AttackController.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Combat/AttackController.cs), se redujo el impulso de zancada de `4.8f` a un paso marcial sobrio de `1.0f` (`~0.35m`), evitando desplazamientos desmedidos durante tajos rápidos.
    * En [`PlayerMovement.Physics.cs`](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Movement/PlayerMovement.Physics.cs), se añadió restricción vertical estricta durante el ataque (`Mathf.Min(0f, _rb.linearVelocity.y)`) y desaceleración rápida (`8.5f * Time.fixedDeltaTime`), impidiendo cualquier elevación física o rebote sobre el suelo.
  * **Validación de Compilación y Restricciones**:
    * Compilación con **0 errores** en `Assembly-CSharp.csproj` y `Assembly-CSharp-Editor.csproj`.
    * Cumplimiento riguroso de [`DEVELOPMENT_CONSTRAINTS.md`](file:///c:/Users/mauro/forge-game/DEVELOPMENT_CONSTRAINTS.md): todos los archivos `<= 250` líneas.



