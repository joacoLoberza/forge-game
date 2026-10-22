# ⚒️ Forge Game

> **Juego de forja y exploración**: Extrae minerales de minas habitadas por monstruos, experimenta con aleaciones metalúrgicas en el horno, forja en el yunque y ensambla armas y armaduras legendarias.

---

## 📌 REGLAS OBLIGATORIAS DE ARQUITECTURA Y DESARROLLO

> ⚠️ **Documentación Detallada**: Consulta la guía técnica completa en [DEVELOPMENT_CONSTRAINTS.md](DEVELOPMENT_CONSTRAINTS.md).

Todo código agregado o modificado en este proyecto debe cumplir estrictamente con los siguientes tres pilares:

### 1. 📏 Límite Estricto de Líneas por Archivo (150 a 250 Líneas)
* **Ningún archivo de código (.cs) debe superar las 250 líneas.** El rango óptimo es de **150 a 200 líneas**.
* **MonoBehaviours de Escena**: Cuando un componente de Unity crezca, debe dividirse utilizando clases parciales (`public partial class`) con nombres descriptivos (ej. `FurnaceController.cs`, `FurnaceController.Ores.cs`, `FurnaceController.Smelting.cs`, `FurnaceController.Thermal.cs`). Esto preserva intactos los GUIDs de Unity y los enlaces del Inspector.
* **Modelos y Lógica Pura**: Enums, estructuras y cálculos matemáticos deben residir en archivos dedicados e independientes.

### 2. 🏛️ Uso Obligatorio de Principios SOLID
* **S (Single Responsibility)**: Controladores procesan la simulación; los paneles de UI solo gestionan presentación y eventos de usuario.
* **O (Open/Closed)**: Nuevas estaciones heredan de `BaseForgeStationController`; nuevos combustibles implementan `IFuelSource`.
* **L (Liskov Substitution)**: Las estaciones derivadas pueden ser operadas indistintamente por el sistema central de estaciones sin conocer su tipo concreto.
* **I (Interface Segregation)**: Interfaces pequeñas y atómicas (`IStationController`, `IDamageable`, `IFuelSource`, `IInventoryReceiver`).
* **D (Dependency Inversion)**: Desacoplamiento mediante eventos de C# (`event Action<T>`) e interfaces en lugar de referencias rígidas entre controladores y UI.

### 3. 🌐 Consideraciones para Implementación Multijugador (Multiplayer)
* **Autoridad del Servidor (Server-Authoritative)**:
  * La simulación térmica del horno, la calidad/pureza del lingote y los estados del inventario deben calcularse exclusivamente en el servidor. El cliente solo envía RPCs de intención (`AddCoal`, `VentGate`, `InsertOre`).
* **Bloqueo Concurrente de Estaciones**:
  * Cada estación registra a su interactor activo (`CurrentInteractor` / `ClientId`). Si un herrero está ocupando el horno, la estación queda bloqueada para otros jugadores (`IsStationOccupied`), impidiendo condiciones de carrera y robo de materiales.
* **Desacoplamiento de Controles**:
  * Ocupar una estación apaga el input map de exploración (`SetMovementEnabled(false)`). De este modo, la tecla `[Espacio]` dentro del horno ventila la compuerta térmica sin provocar que el herrero salte o ataque en el mundo compartido.

---

## 🗂️ Estructura del Módulo de Forja (`Assets/Scripts/MainGame/Forge/`)

Los scripts de forja se organizan en subdirectorios temáticos:

```text
Assets/Scripts/MainGame/Forge/
├── Horno/          # FurnaceController (dividido en 4 partials) y FurnaceStationController
├── Minerales/      # MetalProperties, AlloyComposition, AlloyNameRegistry y ForgeDataModels
├── Yunque/         # AnvilController, AnvilHammerInput y AnvilStationController
├── Prensa/         # PressController y PressStationController
├── Ensamblaje/     # WeaponAssemblyController y modificadores de pureza metalúrgica
└── Estaciones/     # BaseForgeStationController, ForgeController y ForgeStationController
```

---

## 🎮 Controles Principales

### En Exploración y Minas:
* **`W`, `A`, `S`, `D`**: Movimiento del personaje.
* **`[Espacio]`**: Saltar (con detección física de suelo).
* **`Click Izquierdo`**: Atacar enemigos y picar minerales (bloqueado si el cursor está sobre botones de UI).
* **`Q`**: Interactuar con estaciones de trabajo y objetos.

### En la Forja (Horno):
* **`Añadir Carbón` (Botón UI)**: Aumenta la temperatura de forma gradual ($15^\circ\text{C}/\text{s}$).
* **`Mantener [Espacio]`**: Abre la compuerta de ventilación para reducir el exceso de calor.
* **`Añadir Minerales` (Botones UI)**: Añade Hierro, Cobre u Oro si existen en el inventario.
* **`Zona Dorada`**: Resplandor central en el termómetro que acelera la fundición al **+60%** manteniendo pureza perfecta.
