# Tanques

Proyecto de **Programación de Videojuegos** (Prof. Iván Mendoza): videojuego para 2 jugadores en 3D ortográfico, hecho en Unity 6 siguiendo la guía *"Videojuego para 2 jugadores en 3D ortográfico"* (basada en el *Tanks! Tutorial* de Unity).

**Alumno:** Christopher Payero Estrella — **Matrícula:** 1-21-0909

**Jugar en línea:** https://christ2723.github.io/Tanques/

## Controles

| Jugador | Moverse | Disparar (mantener para más fuerza) |
|---|---|---|
| Player 1 (azul) | W A S D | Espacio |
| Player 2 (rojo) | Flechas | Enter |

En la pantalla final: **Enter** o **R** (o el botón) para jugar de nuevo.

## Actividad

1. **Nombre y matrícula** en la esquina superior derecha del juego.
2. Código publicado en **GitHub** y juego publicado en **GitHub Pages**.
3. **Música de fondo** al iniciar el juego: marcha fúnebre (Chopin, Sonata n.º 2) en bucle.
4. El juego **termina cuando un jugador pierde 5 veces** (cada vez que su tanque es destruido, por el rival o por los enemigos).
5. **Pantalla del ganador** con su puntaje (rondas ganadas) y el tiempo que le tomó ganar la partida.
6. **Temporizador de 4 minutos**: si llega a 0 y nadie ha terminado, **ambos jugadores pierden**.
7. **Enemigos**: 4 tanques negros con ojo rojo que patrullan el mapa, persiguen al tanque más cercano, le disparan y le hacen daño al chocarlo. Se pueden destruir con las bombas y reaparecen a los 8 segundos.

## Estructura (como en la guía)

| Script | Función |
|---|---|
| `Tank/TankMovement` | Movimiento, giro y audio del motor |
| `Tank/TankHealth` | Salud, anillo de vida y explosión |
| `Tank/TankShooting` | Carga y disparo de bombas con el AimSlider |
| `Shell/ShellExplosion` | Daño y fuerza por distancia (también daña a los enemigos) |
| `Camera/CameraControl` | Sigue a los tanques y ajusta el zoom ortográfico |
| `UI/UIDirectionControl` | Mantiene fijo el anillo de vida |
| `Managers/GameManager` | Rondas (RoundStarting / RoundPlaying / RoundEnding), derrotas, temporizador y pantalla final |
| `Managers/TankManager` | Configuración y control de cada tanque |
| `Extras/Enemigo` | Enemigos de la actividad 7 |
| `Editor/ConstructorProyecto` | Genera los modelos, sonidos, sprites, prefabs, la escena `Principal` y compila el juego (menú **Tanques**) |

Como el paquete *Tanks! Tutorial* de la Asset Store solo se descarga con una cuenta de Unity, los modelos (tanque, bomba, escenario), las partículas y los sonidos se generan desde `ConstructorProyecto` con los mismos nombres del tutorial (`Tank`, `Shell`, `ShellExplosion`, `TankExplosion`, `DustTrail`, `LevelArt`, `HealthWheel`, `AimArrow`, `EngineIdle`, `ShotFiring`, etc.).

## Cómo abrirlo

1. Abrir la carpeta con Unity 6 (6000.6.4f1) desde Unity Hub.
2. Abrir `Assets/Scenes/Principal.unity` y darle Play.
3. Para regenerar todo: menú **Tanques > Construir todo**. Para compilar: **Tanques > Compilar WebGL** o **Compilar Windows**.
