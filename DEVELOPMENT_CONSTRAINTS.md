# 📋 Restricciones de Desarrollo y Arquitectura del Proyecto (Forge Game)

Este documento establece las **reglas obligatorias de ingeniería de software** para el desarrollo, refactorización y mantenimiento del código fuente en el proyecto **Forge Game**. Todo código agregado o modificado debe cumplir estrictamente con estas especificaciones.

---

## 1. 📏 Límite de Líneas por Archivo (150 a 250 Líneas)

### Regla Fundamental:
> **Ningún archivo de código (.cs) debe exceder las 250 líneas de código.** El tamaño ideal por archivo es de **150 a 200 líneas**.

### Estrategias Obligatorias de Modularización:
1. **Clases Parciales (`public partial class`) para MonoBehaviours de Escena**:
   * Cuando un componente de Unity (`MonoBehaviour`) supere las 200 líneas, **NO** debe fragmentarse en múltiples MonoBehaviours artificiales si están fuertemente acoplados a la misma entidad física o GameObject en escena.
   * En su lugar, debe dividirse utilizando clases parciales con sufijos descriptivos (`Clase.Submodulo.cs`).
   * **Ejemplo implementado en el Horno**:
     * `FurnaceController.cs` (204 líneas): Ciclo de vida, inspectores y simulación térmica.
     * `FurnaceController.Ores.cs` (149 líneas): Gestión de minerales, consultas térmicas y metales duros/blandos.
     * `FurnaceController.Smelting.cs` (109 líneas): Control de fundición, reset y generación de lupas.
     * `FurnaceController.Thermal.cs` (52 líneas): Ventilación de compuertas y combustible.
   * **Ventaja crítica**: Unity compila todas las partes en un único tipo C#, manteniendo intacto el GUID del archivo `.meta` y evitando que se rompan referencias en escenas o prefabs.
2. **Segregación de Modelos de Datos y Lógica Pura**:
   * Las estructuras de datos (`struct`, `class`), enums y cálculos puros (como `AlloyComposition`, `MetalProperties`, `AlloyNameRegistry`) deben extraerse en sus propios archivos dedicados dentro de subcarpetas temáticas (`Minerales/`).
3. **Componentes con Responsabilidad Única**:
   * Si una clase realiza múltiples tareas independientes (por ejemplo, detectar input del jugador, procesar lógica física y actualizar UI), debe dividirse inmediatamente en componentes desacoplados.

---

## 2. 🏛️ Uso Obligatorio de Principios SOLID

El diseño de código debe adherirse de forma rigurosa a los 5 principios SOLID:

### S — Single Responsibility Principle (Responsabilidad Única)
* Cada clase debe tener una única razón para cambiar:
  * **Controladores de Estación** (`AnvilController`, `FurnaceController`, `PressController`): Manejan exclusivamente la física y simulación del proceso de herrería.
  * **Paneles de UI** (`FurnacePanel`, `AnvilPanel`): Solo gestionan presentación visual, retroalimentación al jugador y redirección de intenciones de entrada hacia los controladores.
  * **Modelos de Datos** (`BloomData`, `WeaponData`, `AlloyComposition`): Clases de datos inmutables o POCOs sin lógica de presentación ni dependencias de Unity UI.

### O — Open/Closed Principle (Abierto para Extensión, Cerrado para Modificación)
* Los sistemas deben admitir nuevos elementos sin modificar el código base existente:
  * **Estaciones de Forja**: Heredan de [BaseForgeStationController.cs](file:///c:/Users/mauro/forge-game/Assets/Scripts/MainGame/Forge/Estaciones/BaseForgeStationController.cs). Crear una nueva estación (ej. Templado en Aceite, Afilador) solo requiere heredar e implementar `OnPlayerEnterStation()` y `OnPlayerExitStation()`.
  * **Fuentes de Combustible**: Utilizan la interfaz `IFuelSource`. Agregar carbón mágico, madera o gas no altera el algoritmo térmico del horno.

### L — Liskov Substitution Principle (Sustitución de Liskov)
* Cualquier subclase de estación debe poder ser sustituida por su clase base `BaseForgeStationController` o interfaz `IStationController` sin alterar el comportamiento del router de menús o del sistema de interacción del jugador.
* `BaseForgeStationController.IsAnyStationOccupied()` opera indistintamente sobre hornos, yunques o prensas de manera polimórfica.

### I — Interface Segregation Principle (Segregación de Interfaces)
* Las interfaces deben ser atómicas, específicas y enfocadas:
  * `IStationController`: Contrato exclusivo para ocupación y liberación de estaciones.
  * `IDamageable`: Contrato exclusivo para recibir daño y salud (rocas de mineral, enemigos).
  * `IFuelSource`: Contrato térmico exclusivo para fuentes de calor.
  * `IInventoryReceiver`: Contrato de inserción de ítems.
* Ningún componente debe verse obligado a implementar métodos que no utiliza.

### D — Dependency Inversion Principle (Inversión de Dependencias)
* Los módulos de alto nivel (gestores de menú, controladores de interacción) dependen de abstracciones (`IStationController`, eventos C# como `Action<float>`), no de clases concretas:
  * La UI (`FurnacePanel`) se suscribe a los eventos `TemperatureChanged` y `SmeltProgressChanged` del horno sin que el horno conozca la existencia de la interfaz gráfica.

---

## 3. 🌐 Arquitectura y Consideraciones para Multijugador (Multiplayer)

El sistema de forja y combate está estructurado para permitir una transición directa a redes (ej. **Unity Netcode for GameObjects**, **Fish-Networking** o **Mirror**):

### 1. Autoridad Centralizada del Servidor (Server-Authoritative State)
* **Simulación Física y Térmica en el Servidor**:
  * La temperatura del horno (`CurrentTemperature`), el avance de fundición (`SmeltProgress`) y la pureza metalúrgica (`CurrentImpurityPercentage`) deben calcularse **exclusivamente en el servidor**.
  * El cliente envía intenciones de input mediante RPCs:
    * `AddCoalServerRpc()`
    * `SetVentGateOpenServerRpc(bool isOpen)`
    * `InsertOreServerRpc(MetalType metal, int count)`
* **Clientes como Vistas Reactivas**:
  * Los paneles de UI solo muestran el estado canónico replicado a través de variables de red (`NetworkVariable<float>` para temperatura, `NetworkVariable<int>` para minerales).

### 2. Bloqueo Concurrente de Estaciones (`IsStationOccupied`)
* Cada estación (`Furnace`, `Anvil`, `Press`) cuenta con un registro estricto de ocupante:
  ```csharp
  public bool IsStationOccupied { get; }
  public GameObject CurrentInteractor { get; }
  ```
* **En Red**:
  * Si un jugador (`ClientId 1`) abre la forja, el servidor marca la estación como ocupada por `ClientId 1`.
  * Si otro jugador (`ClientId 2`) se acerca e intenta interactuar, la petición es rechazada de inmediato, impidiendo condiciones de carrera o manipulación cruzada de minerales.
  * Las recompensas finales (ej. la Lupa o el Arma forjada) se entregan directamente al `InventoryController` del interactor activo registrado, evitando robos de ítems entre jugadores.

### 3. Desacoplamiento Estricto: Inputs de Forja vs Mundo Exterior
* Como los inputs dentro de la forja (`[Espacio]` para abrir compuertas) conviven con los inputs de exploración (`[Espacio]` para saltar, `Click` para atacar):
  * La ocupación de la estación desactiva localmente el input map de exploración (`SetMovementEnabled(false)`).
  * El jugador remoto verá al herrero quieto frente al horno mientras opera la interfaz, sin desincronizaciones de posición ni animaciones erráticas de salto.

---

## 4. 🗂️ Organización de Archivos en `Assets/Scripts/MainGame/Forge/`

La carpeta `Forge` debe mantener obligatoriamente la siguiente estructura de subdirectorios temáticos:

```text
Assets/Scripts/MainGame/Forge/
├── Horno/
│   ├── FurnaceController.cs             # Ciclo de vida y simulación térmica
│   ├── FurnaceController.Ores.cs        # Gestión e inserción de minerales
│   ├── FurnaceController.Smelting.cs    # Lógica de fundición y lupas
│   ├── FurnaceController.Thermal.cs     # Ventilación de compuerta y carbón
│   └── FurnaceStationController.cs      # Interacción de la estación del horno
├── Minerales/
│   ├── AlloyComposition.cs             # Cálculo de proporciones y recetas
│   ├── AlloyNameRegistry.cs            # Lore y nombres de aleaciones
│   ├── ForgeDataModels.cs              # Enums y modelos de datos
│   └── MetalProperties.cs              # Propiedades físicas y térmicas de metales
├── Yunque/
│   ├── AnvilController.cs              # Deformación de malla y forjado
│   ├── AnvilHammerInput.cs             # Lectura de martilleo del jugador
│   └── AnvilStationController.cs       # Interacción de la estación del yunque
├── Prensa/
│   ├── PressController.cs              # Prensado de placas y lingotes
│   └── PressStationController.cs       # Interacción de la estación de la prensa
├── Ensamblaje/
│   └── WeaponAssemblyController.cs     # Ensamble final de armas y modificadores de pureza
└── Estaciones/
    ├── BaseForgeStationController.cs   # Clase base abstracta e interfaz IStationController
    ├── ForgeController.cs              # Coordinador general de la herrería
    └── ForgeStationController.cs       # Estación de prueba y compatibilidad
```

---

## 5. 📦 Arquitectura Obligatoria con ScriptableObjects (SO)

Para garantizar modularidad, extensibilidad de datos (OCP) y desacoplamiento entre datos de diseño y lógica de ejecución:

### 1. Definición de Ítems y Recetas (`ItemDefinitionSO`, `WeaponRecipeSO`)
* **Datos Inmutables de Catálogo**:
  * Todo recurso base, mineral, combustible, receta de arma (Espada, Hacha, Martillo) y tipo de mango debe disponer de una plantilla o definición basada en `ScriptableObject`.
  * Debe contener metadatos de presentación: nombre para mostrar, descripción lore, categoría, **sprites/ilustraciones estilo diario**, y prefabricados 3D asociados.
* **Separación entre Plantilla (SO) e Instancia en Tiempo de Ejecución (Runtime Instance)**:
  * Los `ScriptableObjects` son inmutables durante la partida y actúan como "Blueprints" o recetas maestras.
  * Los ítems generados dinámicamente por el jugador (`BloomData`, `IngotData`, `WeaponStats`) son clases serializables C# normales que guardan su estado mutable (pureza, aleación porcentual, deformaciones de malla personalizadas, calidad de forja). Cada instancia dinámica guarda una referencia a su definición base mediante su ID o receta asociada.

### 2. Configuraciones y Preferencias del Jugador (`PlayerSettingsSO` / `GameSettingsSO`)
* Las configuraciones globales del jugador (volumen de audio Master/SFX/Música, sensibilidad de mouse/cámara, asignación de teclas y accesibilidad) deben encapsularse en ScriptableObjects dedicados.
* Facilita la inyección de dependencias y el guardado/carga persistente sin depender de `PlayerPrefs` dispersos en el código.

### 3. Presets de Calibración de Forja (`ForgeCalibrationSO`)
* Los parámetros de equilibrio físico y térmico (temperaturas de fusión de metales, curvas de resistencia de la prensa, número óptimo de golpes en el yunque) deben configurarse en ScriptableObjects de calibración.
* Permite a los diseñadores ajustar el gameplay en el Inspector sin recompilar código ni arriesgar regresiones en la lógica matemática.

---

## 6. ✅ Checklist de Validación para Nuevas Funcionalidades

Antes de dar por completada cualquier tarea en el módulo de forja, verificar:
* [ ] ¿El archivo tiene **menos de 250 líneas**?
* [ ] ¿Cumple con la responsabilidad única y no mezcla UI con simulación matemática?
* [ ] ¿Las interfaces son pequeñas y específicas?
* [ ] ¿Se preservaron los archivos `.meta` al mover o renombrar componentes de Unity?
* [ ] ¿La lógica contempla que múltiples clientes puedan estar conectados sin compartir estados estáticos locales de jugador?
* [ ] ¿Los datos de catálogo, recetas y configuraciones utilizan `ScriptableObjects` en lugar de valores duros en código (`hardcoded`)?
