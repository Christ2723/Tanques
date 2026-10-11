using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//Construye automáticamente todo el juego de Tanques siguiendo la guía del profesor:
//capas, ejes de entrada, iluminación, sonidos, sprites, modelos, prefabs y la escena Principal.
//Como el paquete "Tanks! Tutorial" de la Asset Store no se puede descargar sin la cuenta de Unity,
//los modelos (tanque, bomba, escenario) y los sonidos se generan aquí con los mismos nombres del tutorial.
//Se ejecuta desde el menú "Tanques" de Unity o por línea de comandos.
public static class ConstructorProyecto {

	const string NOMBRE = "Christopher Payero Estrella";
	const string MATRICULA = "1-21-0909";

	const int CAPA_PLAYERS = 8;
	const int CAPA_ENEMIGOS = 9;
	const int FRECUENCIA = 22050;

	static readonly string[] escenas = { "Principal" };

	static Font fuente;
	static Material matArena, matAcantilado, matRoca, matEdificio, matTecho, matHangar, matMetal, matMadera,
		matTanque, matEnemigo, matEnemigoOjo, matBomba, matFuego, matHumo, matPolvo;
	static AudioClip clipIdle, clipDriving, clipCharging, clipFiring, clipShellExplosion, clipTankExplosion, clipMarcha;
	static Sprite spriteHealthWheel, spriteAimArrow;
	static GameObject prefabDustTrail, prefabShellExplosion, prefabTankExplosion, prefabTank, prefabEnemigo, prefabLevelArt;
	static Rigidbody prefabShell;

	[MenuItem("Tanques/Construir todo")]
	public static void ConstruirTodo() {

		foreach (var carpeta in new[] { "Scenes", "Prefabs", "Materials", "AudioClips", "Sprites", "Textures" }) {
			if (!AssetDatabase.IsValidFolder("Assets/" + carpeta)) AssetDatabase.CreateFolder("Assets", carpeta);
		}

		fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

		ConfigurarCapas();
		ConfigurarEjes();
		CrearMateriales();
		CrearAudio();
		CrearSprites();
		CrearPrefabs();
		CrearEscenaPrincipal();

		EditorBuildSettings.scenes = escenas
			.Select(e => new EditorBuildSettingsScene("Assets/Scenes/" + e + ".unity", true))
			.ToArray();

		PlayerSettings.companyName = NOMBRE;
		PlayerSettings.productName = "Tanques";
		PlayerSettings.defaultWebScreenWidth = 1280;
		PlayerSettings.defaultWebScreenHeight = 720;
		PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
		PlayerSettings.WebGL.decompressionFallback = false;

		AssetDatabase.SaveAssets();
		Debug.Log("ConstructorProyecto: todo construido");
	}

	[MenuItem("Tanques/Compilar WebGL")]
	public static void CompilarWebGL() {
		Compilar(BuildTarget.WebGL, "Builds/WebGL");
	}

	[MenuItem("Tanques/Compilar Windows")]
	public static void CompilarWindows() {
		Compilar(BuildTarget.StandaloneWindows64, "Builds/Windows/Tanques.exe");
	}

	static void Compilar(BuildTarget destino, string ruta) {

		var opciones = new BuildPlayerOptions {
			scenes = escenas.Select(e => "Assets/Scenes/" + e + ".unity").ToArray(),
			locationPathName = ruta,
			target = destino,
			options = BuildOptions.None
		};
		var reporte = BuildPipeline.BuildPlayer(opciones);
		Debug.Log("ConstructorProyecto: build " + destino + " " + reporte.summary.result);
		if (reporte.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode) {
			EditorApplication.Exit(1);
		}
	}

	// ---------------------------------------------------------------- Configuración del proyecto

	//Capa "Players" para los tanques (como en la guía) y "Enemigos" para la actividad 7
	static void ConfigurarCapas() {

		var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
		var capas = tagManager.FindProperty("layers");
		capas.GetArrayElementAtIndex(CAPA_PLAYERS).stringValue = "Players";
		capas.GetArrayElementAtIndex(CAPA_ENEMIGOS).stringValue = "Enemigos";
		tagManager.ApplyModifiedProperties();
	}

	//Ejes que usan los scripts: Horizontal1/Vertical1/Fire1 (WASD + Espacio),
	//Horizontal2/Vertical2/Fire2 (flechas + Enter) y HorizontalUI/VerticalUI para el EventSystem
	static void ConfigurarEjes() {

		var inputManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
		var ejes = inputManager.FindProperty("m_Axes");
		ejes.ClearArray();

		void Eje(string nombre, string negativo, string positivo, string altNegativo = "", string altPositivo = "") {
			ejes.arraySize++;
			var e = ejes.GetArrayElementAtIndex(ejes.arraySize - 1);
			e.FindPropertyRelative("m_Name").stringValue = nombre;
			e.FindPropertyRelative("descriptiveName").stringValue = "";
			e.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
			e.FindPropertyRelative("negativeButton").stringValue = negativo;
			e.FindPropertyRelative("positiveButton").stringValue = positivo;
			e.FindPropertyRelative("altNegativeButton").stringValue = altNegativo;
			e.FindPropertyRelative("altPositiveButton").stringValue = altPositivo;
			e.FindPropertyRelative("gravity").floatValue = 3f;
			e.FindPropertyRelative("dead").floatValue = 0.001f;
			e.FindPropertyRelative("sensitivity").floatValue = 3f;
			e.FindPropertyRelative("snap").boolValue = true;
			e.FindPropertyRelative("invert").boolValue = false;
			e.FindPropertyRelative("type").intValue = 0;
			e.FindPropertyRelative("axis").intValue = 0;
			e.FindPropertyRelative("joyNum").intValue = 0;
		}

		Eje("Horizontal1", "a", "d");
		Eje("Vertical1", "s", "w");
		Eje("Fire1", "", "space");
		Eje("Horizontal2", "left", "right");
		Eje("Vertical2", "down", "up");
		Eje("Fire2", "", "return", "", "enter");
		Eje("HorizontalUI", "left", "right", "a", "d");
		Eje("VerticalUI", "down", "up", "s", "w");
		Eje("Submit", "", "return", "", "enter");
		Eje("Cancel", "", "escape");

		inputManager.ApplyModifiedProperties();
	}

	// ---------------------------------------------------------------- Materiales

	static Material CrearMaterial(string nombre, Color color, float metalico = 0f, float suavidad = 0.15f) {

		string ruta = "Assets/Materials/" + nombre + ".mat";
		var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
		if (mat == null) {
			mat = new Material(Shader.Find("Standard"));
			AssetDatabase.CreateAsset(mat, ruta);
		}
		mat.color = color;
		mat.SetFloat("_Metallic", metalico);
		mat.SetFloat("_Glossiness", suavidad);
		EditorUtility.SetDirty(mat);
		return mat;
	}

	static Material CrearMaterialParticulas(string nombre, bool aditivo, Texture2D textura) {

		string ruta = "Assets/Materials/" + nombre + ".mat";
		var shader = Shader.Find(aditivo ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
		if (shader == null) shader = Shader.Find("Sprites/Default");

		var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
		if (mat == null) {
			mat = new Material(shader);
			AssetDatabase.CreateAsset(mat, ruta);
		}
		mat.shader = shader;
		mat.mainTexture = textura;
		if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
		EditorUtility.SetDirty(mat);
		return mat;
	}

	static void CrearMateriales() {

		matArena = CrearMaterial("Sand", new Color(0.96f, 0.76f, 0.47f));
		matAcantilado = CrearMaterial("Cliff", new Color(0.66f, 0.42f, 0.27f));
		matRoca = CrearMaterial("Rock", new Color(0.78f, 0.52f, 0.34f));
		matEdificio = CrearMaterial("Building", new Color(0.93f, 0.9f, 0.84f));
		matTecho = CrearMaterial("Roof", new Color(0.55f, 0.6f, 0.72f));
		matHangar = CrearMaterial("Hangar", new Color(0.86f, 0.86f, 0.9f), 0.2f, 0.4f);
		matMetal = CrearMaterial("Metal", new Color(0.3f, 0.3f, 0.33f), 0.6f, 0.4f);
		matMadera = CrearMaterial("DeadTree", new Color(0.4f, 0.28f, 0.2f));
		matTanque = CrearMaterial("TankColour", new Color(0.4f, 0.65f, 0.3f));
		matEnemigo = CrearMaterial("EnemyBody", new Color(0.16f, 0.15f, 0.18f), 0.4f, 0.35f);
		matEnemigoOjo = CrearMaterial("EnemyEye", new Color(0.9f, 0.1f, 0.1f));
		matEnemigoOjo.EnableKeyword("_EMISSION");
		matEnemigoOjo.SetColor("_EmissionColor", new Color(1.2f, 0.1f, 0.05f));
		matEnemigoOjo.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
		matBomba = CrearMaterial("Shell", new Color(0.85f, 0.65f, 0.2f), 0.5f, 0.6f);

		var particula = CrearTextura("Assets/Textures/Particle.png", 64, 64, (u, v) => {
			float d = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
			float a = (1f - d) * (1f - d);
			return new Color(1, 1, 1, a);
		}, false);

		matFuego = CrearMaterialParticulas("ExplosionFire", true, particula);
		matHumo = CrearMaterialParticulas("ExplosionSmoke", false, particula);
		matPolvo = CrearMaterialParticulas("DustTrail", false, particula);
	}

	// ---------------------------------------------------------------- Texturas y sprites

	static Texture2D CrearTextura(string ruta, int ancho, int alto, Func<float, float, Color> pixel, bool sprite) {

		var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
		const int sub = 3; //supermuestreo para suavizar los bordes
		for (int y = 0; y < alto; y++) {
			for (int x = 0; x < ancho; x++) {
				Color suma = Color.clear;
				for (int sy = 0; sy < sub; sy++) {
					for (int sx = 0; sx < sub; sx++) {
						suma += pixel((x + (sx + 0.5f) / sub) / ancho, (y + (sy + 0.5f) / sub) / alto);
					}
				}
				tex.SetPixel(x, y, suma / (sub * sub));
			}
		}
		File.WriteAllBytes(ruta, tex.EncodeToPNG());
		UnityEngine.Object.DestroyImmediate(tex);

		AssetDatabase.ImportAsset(ruta);
		var importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
		importador.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
		importador.alphaIsTransparency = true;
		importador.mipmapEnabled = false;
		importador.wrapMode = TextureWrapMode.Clamp;
		importador.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
	}

	static void CrearSprites() {

		//HealthWheel: anillo blanco que el slider de salud rellena de forma radial
		CrearTextura("Assets/Sprites/HealthWheel.png", 256, 256, (u, v) => {
			float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
			return new Color(1, 1, 1, d > 0.7f && d < 0.98f ? 1f : 0f);
		}, true);
		spriteHealthWheel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/HealthWheel.png");

		//AimArrow: flecha naranja que crece con la fuerza de disparo
		CrearTextura("Assets/Sprites/AimArrow.png", 128, 256, (u, v) => {
			float x = Mathf.Abs(u - 0.5f);
			bool cuerpo = v < 0.66f && x < 0.14f;
			bool punta = v >= 0.62f && x < 0.5f * (1f - (v - 0.62f) / 0.38f);
			return (cuerpo || punta) ? new Color(1f, 0.55f, 0.12f, 1f) : new Color(1f, 0.55f, 0.12f, 0f);
		}, true);
		spriteAimArrow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AimArrow.png");
	}

	// ---------------------------------------------------------------- Audio

	static void EscribirWav(string ruta, float[] datos) {

		//Normalizo para que el pico quede en 0.9
		float pico = datos.Max(m => Mathf.Abs(m));
		float escala = pico > 0f ? 0.9f / pico : 1f;

		using (var bw = new BinaryWriter(File.Create(ruta))) {
			int bytesDatos = datos.Length * 2;
			bw.Write("RIFF".ToCharArray());
			bw.Write(36 + bytesDatos);
			bw.Write("WAVE".ToCharArray());
			bw.Write("fmt ".ToCharArray());
			bw.Write(16);
			bw.Write((short)1);
			bw.Write((short)1);
			bw.Write(FRECUENCIA);
			bw.Write(FRECUENCIA * 2);
			bw.Write((short)2);
			bw.Write((short)16);
			bw.Write("data".ToCharArray());
			bw.Write(bytesDatos);
			foreach (float m in datos) bw.Write((short)(Mathf.Clamp(m * escala, -1f, 1f) * short.MaxValue));
		}
	}

	static AudioClip CrearClip(string nombre, float segundos, Func<float, System.Random, float> muestra, float cruceBucle = 0f) {

		string ruta = "Assets/AudioClips/" + nombre + ".wav";
		var aleatorio = new System.Random(nombre.GetHashCode());
		int n = (int)(segundos * FRECUENCIA);
		var datos = new float[n];
		for (int i = 0; i < n; i++) datos[i] = muestra(i / (float)FRECUENCIA, aleatorio);

		//Para los sonidos en bucle mezclo el final con el principio y así no se oye el corte
		if (cruceBucle > 0f) {
			int f = (int)(cruceBucle * FRECUENCIA);
			var bucle = new float[n - f];
			for (int i = 0; i < bucle.Length; i++) {
				bucle[i] = datos[i];
				if (i < f) {
					float k = i / (float)f;
					bucle[i] = datos[i] * k + datos[n - f + i] * (1f - k);
				}
			}
			datos = bucle;
		}

		EscribirWav(ruta, datos);
		AssetDatabase.ImportAsset(ruta);
		return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
	}

	static float Seno(float frecuencia, float t) => Mathf.Sin(2f * Mathf.PI * frecuencia * t);

	static float Ruido(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

	static void CrearAudio() {

		//Motor parado: zumbido grave que late
		float lp = 0f;
		clipIdle = CrearClip("EngineIdle", 2.1f, (t, r) => {
			lp += 0.06f * (Ruido(r) - lp);
			float pulso = 0.6f + 0.4f * Seno(12f, t);
			float tono = 0.35f * Seno(45f, t) + 0.2f * Seno(90f, t) + 0.1f * Seno(135f, t);
			return (tono + 1.2f * lp) * pulso;
		}, 0.1f);

		//Motor en marcha: más agudo y con más ruido
		lp = 0f;
		clipDriving = CrearClip("EngineDriving", 2.1f, (t, r) => {
			lp += 0.15f * (Ruido(r) - lp);
			float pulso = 0.55f + 0.45f * Seno(20f, t);
			float tono = 0.3f * Seno(70f, t) + 0.25f * Seno(140f, t) + 0.12f * Seno(210f, t);
			return (tono + 1.0f * lp) * pulso;
		}, 0.1f);

		//Cargando el disparo: tono que sube
		float fase = 0f;
		clipCharging = CrearClip("ShotCharging", 1.0f, (t, r) => {
			float f = 220f + 600f * Mathf.Pow(t, 1.2f);
			fase += 2f * Mathf.PI * f / FRECUENCIA;
			float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1f - t) / 0.05f);
			return (Mathf.Sin(fase) + 0.3f * Mathf.Sin(2f * fase)) * (0.8f + 0.2f * Seno(30f, t)) * env * 0.4f;
		});

		//Disparo: golpe seco con ruido
		lp = 0f; fase = 0f;
		clipFiring = CrearClip("ShotFiring", 0.7f, (t, r) => {
			lp += 0.3f * (Ruido(r) - lp);
			fase += 2f * Mathf.PI * (40f + 120f * Mathf.Exp(-t * 6f)) / FRECUENCIA;
			return lp * Mathf.Exp(-t * 9f) * 1.2f + Mathf.Sin(fase) * Mathf.Exp(-t * 7f) * 0.8f;
		});

		//Explosión de la bomba
		lp = 0f; fase = 0f;
		clipShellExplosion = CrearClip("ShellExplosion", 1.6f, (t, r) => {
			float ruido = Ruido(r);
			lp += 0.12f * (ruido - lp);
			fase += 2f * Mathf.PI * (30f + 70f * Mathf.Exp(-t * 3f)) / FRECUENCIA;
			float chasquido = t < 0.03f ? ruido * (1f - t / 0.03f) * 0.6f : 0f;
			return lp * Mathf.Exp(-t * 3.5f) * 1.6f + Mathf.Sin(fase) * Mathf.Exp(-t * 4f) * 0.9f + chasquido;
		});

		//Explosión del tanque: más larga y con dos estallidos
		lp = 0f; fase = 0f;
		clipTankExplosion = CrearClip("TankExplosion", 2.6f, (t, r) => {
			float ruido = Ruido(r);
			lp += 0.1f * (ruido - lp);
			fase += 2f * Mathf.PI * (25f + 60f * Mathf.Exp(-t * 2f)) / FRECUENCIA;
			float segundo = t > 0.18f ? Mathf.Exp(-(t - 0.18f) * 2.5f) : 0f;
			float chasquido = t < 0.04f ? ruido * (1f - t / 0.04f) * 0.7f : 0f;
			return lp * (Mathf.Exp(-t * 2.2f) + 0.8f * segundo) * 1.6f + Mathf.Sin(fase) * Mathf.Exp(-t * 2.5f) + chasquido;
		});

		CrearMarchaFunebre();
	}

	static float Midi(int nota) => 440f * Mathf.Pow(2f, (nota - 69) / 12f);

	//Actividad 3: Marcha fúnebre (Chopin, Sonata n.º 2, 3.er movimiento) en si bemol menor.
	//Melodía con órgano, acompañamiento de acordes Sib menor / Solb mayor y timbal en cada compás.
	static void CrearMarchaFunebre() {

		const float bpm = 52f;
		float pulso = 60f / bpm;

		//(nota MIDI, duración en pulsos)
		int[,] fraseA1 = { { 70, 100 }, { 70, 75 }, { 70, 25 }, { 70, 200 } };
		int[,] fraseA2 = { { 73, 75 }, { 72, 25 }, { 72, 75 }, { 70, 25 }, { 70, 75 }, { 69, 25 }, { 70, 100 } };
		int[,] fraseB1 = { { 73, 100 }, { 73, 75 }, { 73, 25 }, { 73, 200 } };
		int[,] fraseB2 = { { 77, 75 }, { 75, 25 }, { 75, 75 }, { 73, 25 }, { 73, 75 }, { 72, 25 }, { 73, 100 } };
		var compases = new[] { fraseA1, fraseA2, fraseA1, fraseA2, fraseB1, fraseB2, fraseA1, fraseA2 };

		int totalPulsos = compases.Length * 4;
		float segundos = totalPulsos * pulso;
		int n = (int)(segundos * FRECUENCIA);
		var datos = new float[n];

		void Agregar(float inicio, float duracion, float frecuencia, float volumen, float ataque, float caida, bool organo) {
			int desde = (int)(inicio * FRECUENCIA);
			int largo = (int)((duracion + 0.4f) * FRECUENCIA);
			for (int i = 0; i < largo; i++) {
				int idx = (desde + i) % n; //lo que se pasa del final suena al principio (bucle)
				float t = i / (float)FRECUENCIA;
				float env = Mathf.Clamp01(t / ataque);
				env *= organo ? Mathf.Clamp01((duracion - t) / 0.12f + 1f) * (t < duracion ? 1f : Mathf.Exp(-(t - duracion) * 12f))
					: Mathf.Exp(-t * caida);
				float vibrato = organo ? 1f + 0.004f * Seno(5f, t) : 1f;
				float onda = Seno(frecuencia * vibrato, t) + 0.5f * Seno(frecuencia * 2f * vibrato, t)
					+ 0.25f * Seno(frecuencia * 3f, t) + 0.12f * Seno(frecuencia * 4f, t);
				datos[idx] += onda * env * volumen;
			}
		}

		float tiempo = 0f;
		for (int c = 0; c < compases.Length; c++) {
			float inicioCompas = c * 4 * pulso;
			var frase = compases[c];

			//Melodía (y la misma una octava abajo, más suave)
			for (int i = 0; i < frase.GetLength(0); i++) {
				float dur = frase[i, 1] / 100f * pulso;
				Agregar(tiempo, dur * 0.92f, Midi(frase[i, 0]), 0.16f, 0.03f, 0f, true);
				Agregar(tiempo, dur * 0.92f, Midi(frase[i, 0] - 12), 0.09f, 0.03f, 0f, true);
				tiempo += dur;
			}

			//Acompañamiento: Sib menor y Solb mayor alternados en cada pulso
			for (int p = 0; p < 4; p++) {
				int[] acorde = p % 2 == 0 ? new[] { 46, 53, 58, 61 } : new[] { 42, 49, 54, 58 };
				foreach (int nota in acorde) Agregar(inicioCompas + p * pulso, pulso, Midi(nota), 0.07f, 0.01f, 2.2f, false);
			}

			//Timbal grave al principio de cada compás
			Agregar(inicioCompas, 1.2f, Midi(34), 0.35f, 0.005f, 3f, false);
		}

		//Un poco de eco para que suene como en una sala
		int eco1 = (int)(0.13f * FRECUENCIA), eco2 = (int)(0.21f * FRECUENCIA);
		var conEco = new float[n];
		for (int i = 0; i < n; i++) {
			conEco[i] = datos[i] + 0.3f * conEco[(i - eco1 + n) % n] * (i >= eco1 ? 1f : 0f) + 0.2f * datos[(i - eco2 + n) % n];
		}

		string ruta = "Assets/AudioClips/MarchaFunebre.wav";
		EscribirWav(ruta, conEco);
		AssetDatabase.ImportAsset(ruta);
		clipMarcha = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
	}

	// ---------------------------------------------------------------- Ayudas de modelado

	//Crea una pieza visual sin collider (los colliders van en el objeto raíz, como en la guía)
	static GameObject Pieza(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat,
		Vector3 rot = default, bool conCollider = false) {

		var go = GameObject.CreatePrimitive(tipo);
		go.name = nombre;
		go.transform.SetParent(padre, false);
		go.transform.localPosition = pos;
		go.transform.localEulerAngles = rot;
		go.transform.localScale = escala;
		go.GetComponent<Renderer>().sharedMaterial = mat;
		if (!conCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
		return go;
	}

	static GameObject GuardarPrefab(GameObject go, string nombre) {
		var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/" + nombre + ".prefab");
		UnityEngine.Object.DestroyImmediate(go);
		return prefab;
	}

	static ParticleSystem Particulas(GameObject go, Material mat, bool bucle, float duracion, Vector2 vida, Vector2 velocidad,
		Vector2 tamano, Color colorA, Color colorB, float gravedad, int rafaga) {

		var ps = go.AddComponent<ParticleSystem>();
		ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

		var main = ps.main;
		main.duration = duracion;
		main.loop = bucle;
		main.playOnAwake = bucle;
		main.startLifetime = new ParticleSystem.MinMaxCurve(vida.x, vida.y);
		main.startSpeed = new ParticleSystem.MinMaxCurve(velocidad.x, velocidad.y);
		main.startSize = new ParticleSystem.MinMaxCurve(tamano.x, tamano.y);
		main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
		main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
		main.gravityModifier = gravedad;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.maxParticles = 300;

		var emision = ps.emission;
		emision.rateOverTime = 0f;
		if (rafaga > 0) emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)rafaga) });

		var forma = ps.shape;
		forma.shapeType = ParticleSystemShapeType.Sphere;
		forma.radius = 0.4f;

		var color = ps.colorOverLifetime;
		color.enabled = true;
		var gradiente = new Gradient();
		gradiente.SetKeys(
			new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
			new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
		color.color = new ParticleSystem.MinMaxGradient(gradiente);

		var tam = ps.sizeOverLifetime;
		tam.enabled = true;
		tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

		go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
		return ps;
	}

	static AudioSource Audio(GameObject go, AudioClip clip, bool alArrancar, bool bucle, float volumen = 1f) {
		var a = go.AddComponent<AudioSource>();
		a.clip = clip;
		a.playOnAwake = alArrancar;
		a.loop = bucle;
		a.volume = volumen;
		return a;
	}

	// ---------------------------------------------------------------- Prefabs

	static void CrearPrefabs() {

		CrearDustTrail();
		prefabShellExplosion = CrearExplosion("ShellExplosion", 1f, clipShellExplosion);
		prefabTankExplosion = CrearExplosion("TankExplosion", 2f, clipTankExplosion);
		CrearShell();
		CrearTank();
		CrearEnemigo();
		CrearLevelArt();
	}

	//Polvo que levantan las orugas al moverse
	static void CrearDustTrail() {

		var go = new GameObject("DustTrail");
		var ps = Particulas(go, matPolvo, true, 1f, new Vector2(0.6f, 1f), new Vector2(0.5f, 1.5f), new Vector2(0.4f, 0.9f),
			new Color(0.85f, 0.7f, 0.5f, 0.6f), new Color(0.75f, 0.6f, 0.42f, 0.5f), -0.05f, 0);
		var emision = ps.emission;
		emision.rateOverDistance = 8f;
		var forma = ps.shape;
		forma.shapeType = ParticleSystemShapeType.Cone;
		forma.angle = 15f;
		forma.radius = 0.1f;
		forma.rotation = new Vector3(-90f, 0f, 0f);
		prefabDustTrail = GuardarPrefab(go, "DustTrail");
	}

	//Explosión = fuego (aditivo) + humo (hijo) + fuente de audio, como los prefabs del tutorial
	static GameObject CrearExplosion(string nombre, float escala, AudioClip clip) {

		var go = new GameObject(nombre);
		var fuego = Particulas(go, matFuego, false, 1.5f, new Vector2(0.3f, 0.7f) * Mathf.Sqrt(escala), new Vector2(3f, 8f) * escala,
			new Vector2(0.8f, 1.6f) * escala, new Color(1f, 0.75f, 0.25f), new Color(1f, 0.35f, 0.1f), 0f, (int)(35 * escala));
		var formaFuego = fuego.shape;
		formaFuego.radius = 0.4f * escala;

		var humoGo = new GameObject("Smoke");
		humoGo.transform.SetParent(go.transform, false);
		var humo = Particulas(humoGo, matHumo, false, 1.5f, new Vector2(0.8f, 1.4f) * Mathf.Sqrt(escala), new Vector2(1f, 3f) * escala,
			new Vector2(1.2f, 2.2f) * escala, new Color(0.35f, 0.32f, 0.3f, 0.8f), new Color(0.55f, 0.5f, 0.45f, 0.7f), -0.15f, (int)(20 * escala));
		var formaHumo = humo.shape;
		formaHumo.radius = 0.5f * escala;

		Audio(go, clip, false, false);
		return GuardarPrefab(go, nombre);
	}

	//La bomba (Shell) con su Capsule Collider de trigger, Rigidbody, luz y la explosión como hijo
	static void CrearShell() {

		var go = new GameObject("Shell");
		Pieza(PrimitiveType.Capsule, "ShellMesh", go.transform, new Vector3(0, 0, 0.2f), new Vector3(0.22f, 0.28f, 0.22f), matBomba, new Vector3(90, 0, 0));

		var capsula = go.AddComponent<CapsuleCollider>();
		capsula.isTrigger = true;
		capsula.direction = 2;
		capsula.center = new Vector3(0, 0, 0.2f);
		capsula.radius = 0.15f;
		capsula.height = 0.55f;

		go.AddComponent<Rigidbody>();

		var explosion = (GameObject)PrefabUtility.InstantiatePrefab(prefabShellExplosion, go.transform);

		var luz = go.AddComponent<Light>();
		luz.type = LightType.Point;
		luz.color = new Color(1f, 0.6f, 0.2f);
		luz.range = 3f;
		luz.intensity = 2f;

		var script = go.AddComponent<ShellExplosion>();
		script.m_TankMask = (1 << CAPA_PLAYERS) | (1 << CAPA_ENEMIGOS);
		script.m_ExplosionParticles = explosion.GetComponent<ParticleSystem>();
		script.m_ExplosionAudio = explosion.GetComponent<AudioSource>();

		prefabShell = GuardarPrefab(go, "Shell").GetComponent<Rigidbody>();
	}

	//Slider de UI hecho a mano (como GameObject > UI > Slider, ya con los cambios de la guía)
	static Slider CrearSlider(string nombre, Transform padre, out Image fondo, out Image relleno, bool conFondo) {

		var go = new GameObject(nombre, typeof(RectTransform));
		go.transform.SetParent(padre, false);
		var slider = go.AddComponent<Slider>();
		Estirar(go.GetComponent<RectTransform>());

		fondo = null;
		if (conFondo) {
			var fondoGo = new GameObject("Background", typeof(RectTransform));
			fondoGo.transform.SetParent(go.transform, false);
			Estirar(fondoGo.GetComponent<RectTransform>());
			fondo = fondoGo.AddComponent<Image>();
		}

		var area = new GameObject("Fill Area", typeof(RectTransform));
		area.transform.SetParent(go.transform, false);
		Estirar(area.GetComponent<RectTransform>());

		var fill = new GameObject("Fill", typeof(RectTransform));
		fill.transform.SetParent(area.transform, false);
		Estirar(fill.GetComponent<RectTransform>());
		relleno = fill.AddComponent<Image>();

		slider.interactable = false;
		slider.transition = Selectable.Transition.None;
		slider.navigation = new Navigation { mode = Navigation.Mode.None };
		slider.fillRect = fill.GetComponent<RectTransform>();
		slider.handleRect = null;
		return slider;
	}

	static void Estirar(RectTransform rt) {
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	//El tanque: Rigidbody, Box Collider, 2 Audio Sources, DustTrails, Canvas con HealthSlider y AimSlider,
	//FireTransform y los scripts TankMovement, TankHealth y TankShooting
	static void CrearTank() {

		var tank = new GameObject("Tank");
		tank.layer = CAPA_PLAYERS;

		//Modelo (todas las piezas se pintan con el color del jugador en TankManager.Setup)
		var renderers = new GameObject("TankRenderers").transform;
		renderers.SetParent(tank.transform, false);
		Pieza(PrimitiveType.Cube, "TankChassis", renderers, new Vector3(0, 0.6f, 0), new Vector3(1.25f, 0.55f, 1.55f), matTanque);
		Pieza(PrimitiveType.Cube, "TankTracksLeft", renderers, new Vector3(-0.62f, 0.35f, 0), new Vector3(0.36f, 0.6f, 1.7f), matTanque);
		Pieza(PrimitiveType.Cube, "TankTracksRight", renderers, new Vector3(0.62f, 0.35f, 0), new Vector3(0.36f, 0.6f, 1.7f), matTanque);
		Pieza(PrimitiveType.Cylinder, "TankTurret", renderers, new Vector3(0, 1.1f, -0.05f), new Vector3(0.95f, 0.22f, 0.95f), matTanque);
		Pieza(PrimitiveType.Sphere, "TankTurretTop", renderers, new Vector3(0, 1.3f, -0.05f), new Vector3(0.7f, 0.35f, 0.7f), matTanque);
		Pieza(PrimitiveType.Cylinder, "TankBarrel", renderers, new Vector3(0, 1.25f, 0.8f), new Vector3(0.17f, 0.45f, 0.17f), matTanque, new Vector3(80, 0, 0));
		Pieza(PrimitiveType.Cylinder, "TankAntenna", renderers, new Vector3(-0.3f, 1.65f, -0.35f), new Vector3(0.03f, 0.35f, 0.03f), matTanque);

		var rb = tank.AddComponent<Rigidbody>();
		rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
		rb.linearDamping = 1f;

		var caja = tank.AddComponent<BoxCollider>();
		caja.center = new Vector3(0, 0.85f, 0);
		caja.size = new Vector3(1.5f, 1.7f, 1.6f);

		var audioMotor = Audio(tank, clipIdle, true, true, 0.5f);
		var audioDisparo = Audio(tank, null, false, false);

		//DustTrails
		var izq = (GameObject)PrefabUtility.InstantiatePrefab(prefabDustTrail, tank.transform);
		izq.name = "LeftDustTrail";
		izq.transform.localPosition = new Vector3(-0.5f, 0, -0.75f);
		var der = (GameObject)PrefabUtility.InstantiatePrefab(prefabDustTrail, tank.transform);
		der.name = "RightDustTrail";
		der.transform.localPosition = new Vector3(0.5f, 0, -0.75f);

		var movimiento = tank.AddComponent<TankMovement>();
		movimiento.m_MovementAudio = audioMotor;
		movimiento.m_EngineIdling = clipIdle;
		movimiento.m_EngineDriving = clipDriving;

		//Canvas en World Space, hijo del tanque y tumbado sobre el suelo
		var canvasGo = new GameObject("Canvas", typeof(RectTransform));
		canvasGo.transform.SetParent(tank.transform, false);
		var canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.WorldSpace;
		canvas.referencePixelsPerUnit = 1f;
		var scaler = canvasGo.AddComponent<CanvasScaler>();
		scaler.dynamicPixelsPerUnit = 1f;
		scaler.referencePixelsPerUnit = 1f;
		canvasGo.AddComponent<GraphicRaycaster>();
		var canvasRt = canvasGo.GetComponent<RectTransform>();
		canvasRt.localPosition = new Vector3(0, 0.1f, 0);
		canvasRt.sizeDelta = new Vector2(3.5f, 3.5f);
		canvasRt.localEulerAngles = new Vector3(90, 0, 0);

		//HealthSlider (anillo de vida)
		var salud = CrearSlider("HealthSlider", canvasGo.transform, out var fondo, out var relleno, true);
		fondo.sprite = spriteHealthWheel;
		fondo.color = new Color(1, 1, 1, 80f / 255f);
		relleno.sprite = spriteHealthWheel;
		relleno.color = new Color(1, 1, 1, 150f / 255f);
		relleno.type = Image.Type.Filled;
		relleno.fillMethod = Image.FillMethod.Radial360;
		relleno.fillOrigin = (int)Image.Origin360.Left;
		relleno.fillClockwise = false;
		salud.minValue = 0f;
		salud.maxValue = 100f;
		salud.value = 100f;
		salud.gameObject.AddComponent<UIDirectionControl>();

		//AimSlider (flecha de fuerza de disparo)
		var mira = CrearSlider("AimSlider", canvasGo.transform, out _, out var flecha, false);
		mira.direction = Slider.Direction.BottomToTop;
		mira.minValue = 15f;
		mira.maxValue = 30f;
		mira.value = 15f;
		flecha.sprite = spriteAimArrow;
		var miraRt = mira.GetComponent<RectTransform>();
		miraRt.offsetMin = new Vector2(1f, 3f);
		miraRt.offsetMax = new Vector2(-1f, 9f);
		miraRt.anchoredPosition3D = new Vector3(miraRt.anchoredPosition3D.x, miraRt.anchoredPosition3D.y, -1f);
		var flechaRt = flecha.GetComponent<RectTransform>();
		flechaRt.sizeDelta = new Vector2(flechaRt.sizeDelta.x, 0f);

		var vida = tank.AddComponent<TankHealth>();
		vida.m_Slider = salud;
		vida.m_FillImage = relleno;
		vida.m_ExplosionPrefab = prefabTankExplosion;

		//FireTransform
		var fireTransform = new GameObject("FireTransform").transform;
		fireTransform.SetParent(tank.transform, false);
		fireTransform.localPosition = new Vector3(0, 1.7f, 1.35f);
		fireTransform.localEulerAngles = new Vector3(350, 0, 0);

		var disparo = tank.AddComponent<TankShooting>();
		disparo.m_Shell = prefabShell;
		disparo.m_FireTransform = fireTransform;
		disparo.m_AimSlider = mira;
		disparo.m_ShootingAudio = audioDisparo;
		disparo.m_ChargingClip = clipCharging;
		disparo.m_FireClip = clipFiring;

		prefabTank = GuardarPrefab(tank, "Tank");
	}

	//Actividad 7: tanque enemigo negro con ojo rojo
	static void CrearEnemigo() {

		var go = new GameObject("Enemigo");
		go.layer = CAPA_ENEMIGOS;

		var renderers = new GameObject("EnemigoRenderers").transform;
		renderers.SetParent(go.transform, false);
		Pieza(PrimitiveType.Cube, "Chasis", renderers, new Vector3(0, 0.5f, 0), new Vector3(1.6f, 0.55f, 1.8f), matEnemigo);
		Pieza(PrimitiveType.Cube, "OrugaIzq", renderers, new Vector3(-0.85f, 0.32f, 0), new Vector3(0.3f, 0.55f, 1.9f), matMetal);
		Pieza(PrimitiveType.Cube, "OrugaDer", renderers, new Vector3(0.85f, 0.32f, 0), new Vector3(0.3f, 0.55f, 1.9f), matMetal);
		Pieza(PrimitiveType.Sphere, "Cupula", renderers, new Vector3(0, 0.95f, -0.05f), new Vector3(1.15f, 0.8f, 1.15f), matEnemigo);
		Pieza(PrimitiveType.Sphere, "Ojo", renderers, new Vector3(0, 1.05f, 0.45f), new Vector3(0.38f, 0.38f, 0.38f), matEnemigoOjo);
		Pieza(PrimitiveType.Cylinder, "Canon", renderers, new Vector3(0, 1.1f, 0.85f), new Vector3(0.16f, 0.45f, 0.16f), matMetal, new Vector3(80, 0, 0));
		for (int i = 0; i < 4; i++) {
			float x = i % 2 == 0 ? -0.65f : 0.65f, z = i < 2 ? -0.75f : 0.75f;
			Pieza(PrimitiveType.Cube, "Pincho" + i, renderers, new Vector3(x, 0.85f, z), new Vector3(0.18f, 0.18f, 0.18f), matEnemigoOjo, new Vector3(45, 45, 0));
		}

		var rb = go.AddComponent<Rigidbody>();
		rb.mass = 3f;
		rb.linearDamping = 2f;
		rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

		var caja = go.AddComponent<BoxCollider>();
		caja.center = new Vector3(0, 0.7f, 0);
		caja.size = new Vector3(1.9f, 1.4f, 1.9f);

		var audio = Audio(go, null, false, false, 0.7f);

		var fireTransform = new GameObject("FireTransform").transform;
		fireTransform.SetParent(go.transform, false);
		fireTransform.localPosition = new Vector3(0, 1.45f, 1.45f);
		fireTransform.localEulerAngles = new Vector3(350, 0, 0);

		var enemigo = go.AddComponent<Enemigo>();
		enemigo.m_Shell = prefabShell;
		enemigo.m_FireTransform = fireTransform;
		enemigo.m_ShootingAudio = audio;
		enemigo.m_FireClip = clipFiring;
		enemigo.m_ExplosionPrefab = prefabTankExplosion;
		enemigo.m_ObstacleMask = 1 << 0;

		prefabEnemigo = GuardarPrefab(go, "Enemigo");
	}

	// ---------------------------------------------------------------- Escenario (LevelArt)

	static void Edificio(Transform padre, Vector3 pos, Vector3 tamano, float giro) {

		var b = new GameObject("Building").transform;
		b.SetParent(padre, false);
		b.localPosition = pos;
		b.localEulerAngles = new Vector3(0, giro, 0);
		Pieza(PrimitiveType.Cube, "Walls", b, new Vector3(0, tamano.y / 2f, 0), tamano, matEdificio, default, true);
		Pieza(PrimitiveType.Cube, "Roof", b, new Vector3(0, tamano.y + 0.15f, 0), new Vector3(tamano.x + 0.4f, 0.3f, tamano.z + 0.4f), matTecho);
		//Ventanas y puerta
		Pieza(PrimitiveType.Cube, "Door", b, new Vector3(0, 0.9f, tamano.z / 2f + 0.01f), new Vector3(1.1f, 1.8f, 0.05f), matMetal);
		for (int i = -1; i <= 1; i += 2) {
			Pieza(PrimitiveType.Cube, "Window", b, new Vector3(i * tamano.x * 0.3f, tamano.y * 0.6f, tamano.z / 2f + 0.01f),
				new Vector3(0.9f, 0.8f, 0.05f), matTecho);
		}
	}

	static void Hangar(Transform padre, Vector3 pos, float largo, float radio, float giro) {

		var h = Pieza(PrimitiveType.Cylinder, "Hangar", padre, pos, new Vector3(radio * 2f, largo / 2f, radio * 2f), matHangar,
			new Vector3(90, giro, 0));
		h.AddComponent<BoxCollider>();
	}

	static void Roca(Transform padre, Vector3 pos, float tamano, int semilla) {

		var r = new GameObject("Rocks").transform;
		r.SetParent(padre, false);
		r.localPosition = pos;
		var aleatorio = new System.Random(semilla);
		float R() => (float)aleatorio.NextDouble();
		for (int i = 0; i < 4; i++) {
			var offset = new Vector3((R() - 0.5f) * tamano, 0, (R() - 0.5f) * tamano);
			var escala = new Vector3(tamano * (0.4f + R() * 0.5f), tamano * (0.4f + R() * 0.8f), tamano * (0.4f + R() * 0.5f));
			Pieza(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Rock", r, offset + Vector3.up * escala.y * 0.35f, escala,
				matRoca, new Vector3(R() * 20f, R() * 360f, R() * 20f), true);
		}
	}

	static void Arbol(Transform padre, Vector3 pos, int semilla) {

		var a = new GameObject("DeadTree").transform;
		a.SetParent(padre, false);
		a.localPosition = pos;
		a.localEulerAngles = new Vector3(0, semilla * 47f, 0);
		Pieza(PrimitiveType.Cylinder, "Trunk", a, new Vector3(0, 1.5f, 0), new Vector3(0.35f, 1.5f, 0.35f), matMadera, default, true);
		Pieza(PrimitiveType.Cylinder, "Branch", a, new Vector3(0.45f, 2.6f, 0), new Vector3(0.15f, 0.7f, 0.15f), matMadera, new Vector3(0, 0, -40));
		Pieza(PrimitiveType.Cylinder, "Branch", a, new Vector3(-0.4f, 2.3f, 0.1f), new Vector3(0.12f, 0.55f, 0.12f), matMadera, new Vector3(10, 0, 45));
		Pieza(PrimitiveType.Cylinder, "Branch", a, new Vector3(0, 2.9f, -0.35f), new Vector3(0.1f, 0.5f, 0.1f), matMadera, new Vector3(-40, 0, 0));
	}

	static void Deposito(Transform padre, Vector3 pos) {

		var d = new GameObject("OilStorage").transform;
		d.SetParent(padre, false);
		d.localPosition = pos;
		for (int i = 0; i < 3; i++) {
			Pieza(PrimitiveType.Cylinder, "Tank", d, new Vector3(i * 3.2f, 1.5f, (i % 2) * 2f), new Vector3(2.6f, 1.5f, 2.6f), matHangar, default, true);
		}
	}

	static void Bombeador(Transform padre, Vector3 pos, float giro) {

		var p = new GameObject("PumpJack").transform;
		p.SetParent(padre, false);
		p.localPosition = pos;
		p.localEulerAngles = new Vector3(0, giro, 0);
		Pieza(PrimitiveType.Cube, "Base", p, new Vector3(0, 0.25f, 0), new Vector3(1.5f, 0.5f, 4f), matMetal, default, true);
		Pieza(PrimitiveType.Cube, "Post", p, new Vector3(0, 1.8f, 0), new Vector3(0.4f, 3f, 0.4f), matMetal);
		Pieza(PrimitiveType.Cube, "Beam", p, new Vector3(0, 3.3f, 0.3f), new Vector3(0.3f, 0.35f, 4f), matTecho, new Vector3(-12, 0, 0));
		Pieza(PrimitiveType.Cube, "Head", p, new Vector3(0, 3.1f, 2.3f), new Vector3(0.5f, 1.1f, 0.4f), matTecho, new Vector3(-12, 0, 0));
	}

	static void CrearLevelArt() {

		var nivel = new GameObject("LevelArt").transform;

		//Suelo de arena
		var suelo = Pieza(PrimitiveType.Plane, "Ground", nivel, Vector3.zero, new Vector3(10, 1, 10), matArena, default, true);
		suelo.name = "Ground";

		//Acantilados que rodean el desierto
		var bordes = new GameObject("Cliffs").transform;
		bordes.SetParent(nivel, false);
		for (int lado = 0; lado < 4; lado++) {
			for (int i = -5; i <= 5; i++) {
				float a = i * 9.5f;
				float h = 4f + Mathf.PerlinNoise(lado * 3.1f, i * 0.7f) * 4f;
				Vector3 pos = lado switch {
					0 => new Vector3(a, h / 2f, 48f),
					1 => new Vector3(a, h / 2f, -48f),
					2 => new Vector3(48f, h / 2f, a),
					_ => new Vector3(-48f, h / 2f, a),
				};
				Pieza(PrimitiveType.Cube, "Cliff", bordes, pos, new Vector3(10.5f, h, 10.5f), matAcantilado,
					new Vector3(0, (i * 13 + lado * 7) % 20 - 10, 0), true);
			}
		}

		//Edificios
		var edificios = new GameObject("Buildings").transform;
		edificios.SetParent(nivel, false);
		Edificio(edificios, new Vector3(-22, 0, 22), new Vector3(8, 4.5f, 6), 15);
		Edificio(edificios, new Vector3(20, 0, 28), new Vector3(6, 4, 6), -10);
		Edificio(edificios, new Vector3(30, 0, -12), new Vector3(7, 5, 5), 30);
		Edificio(edificios, new Vector3(-12, 0, -24), new Vector3(8, 4, 6), 0);
		Edificio(edificios, new Vector3(-34, 0, -28), new Vector3(6, 4, 6), 45);
		Hangar(edificios, new Vector3(-36, 0, 8), 12f, 3f, 0);
		Hangar(edificios, new Vector3(-36, 0, -4), 12f, 3f, 0);
		Hangar(edificios, new Vector3(34, 0, 26), 10f, 2.8f, 0);

		//Rocas, árboles secos, depósitos y bombeadores de petróleo
		var detalles = new GameObject("Props").transform;
		detalles.SetParent(nivel, false);
		Roca(detalles, new Vector3(-9, 0, 14), 3.5f, 1);
		Roca(detalles, new Vector3(17, 0, 6), 3f, 2);
		Roca(detalles, new Vector3(-2, 0, -9), 3f, 3);
		Roca(detalles, new Vector3(13, 0, 21), 3.5f, 4);
		Roca(detalles, new Vector3(-16, 0, -6), 3f, 5);
		Roca(detalles, new Vector3(24, 0, -30), 4f, 6);
		Arbol(detalles, new Vector3(-11, 0, 31), 1);
		Arbol(detalles, new Vector3(9, 0, -15), 2);
		Arbol(detalles, new Vector3(31, 0, 2), 3);
		Arbol(detalles, new Vector3(-26, 0, -14), 4);
		Arbol(detalles, new Vector3(4, 0, 34), 5);
		Deposito(detalles, new Vector3(-33, 0, 30));
		Deposito(detalles, new Vector3(30, 0, -34));
		Bombeador(detalles, new Vector3(16, 0, 39), 90);
		Bombeador(detalles, new Vector3(-4, 0, -37), 0);

		//Luz del escenario (sustituye a la Directional Light que se borra en la guía)
		var luz = new GameObject("Directional Light").AddComponent<Light>();
		luz.transform.SetParent(nivel, false);
		luz.type = LightType.Directional;
		luz.color = new Color(1f, 0.95f, 0.85f);
		luz.intensity = 1.2f;
		luz.shadows = LightShadows.Soft;
		luz.transform.localEulerAngles = new Vector3(50, -30, 0);

		prefabLevelArt = GuardarPrefab(nivel.gameObject, "LevelArt");
	}

	// ---------------------------------------------------------------- UI de pantalla

	static Text Texto(Transform padre, string nombre, string contenido, int tamano, TextAnchor alineacion,
		Vector2 anclaMin, Vector2 anclaMax, Vector2 pivote, Vector2 posicion, Vector2 tamanoRect) {

		var go = new GameObject(nombre, typeof(RectTransform));
		go.transform.SetParent(padre, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = anclaMin;
		rt.anchorMax = anclaMax;
		rt.pivot = pivote;
		rt.anchoredPosition = posicion;
		rt.sizeDelta = tamanoRect;

		var t = go.AddComponent<Text>();
		t.font = fuente;
		t.text = contenido;
		t.fontSize = tamano;
		t.fontStyle = FontStyle.Bold;
		t.alignment = alineacion;
		t.color = Color.white;
		t.supportRichText = true;
		t.horizontalOverflow = HorizontalWrapMode.Wrap;
		t.verticalOverflow = VerticalWrapMode.Overflow;
		t.raycastTarget = false;

		var sombra = go.AddComponent<Shadow>();
		sombra.effectColor = new Color(114f / 255f, 71f / 255f, 40f / 255f, 128f / 255f);
		sombra.effectDistance = new Vector2(-3, 3);
		return t;
	}

	static Canvas CanvasPantalla(string nombre, int orden) {

		var go = new GameObject(nombre, typeof(RectTransform));
		var canvas = go.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = orden;
		var scaler = go.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		go.AddComponent<GraphicRaycaster>();
		return canvas;
	}

	// ---------------------------------------------------------------- Escena Principal

	static void CrearEscenaPrincipal() {

		var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		//Iluminación: sin GI horneada, ambiente de color RGB 72,62,113
		var iluminacion = new LightingSettings { bakedGI = false, realtimeGI = false };
		AssetDatabase.CreateAsset(iluminacion, "Assets/Scenes/PrincipalLighting.lighting");
		Lightmapping.lightingSettings = iluminacion;
		RenderSettings.ambientMode = AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(72f / 255f, 62f / 255f, 113f / 255f);
		RenderSettings.skybox = null;
		RenderSettings.fog = false;

		PrefabUtility.InstantiatePrefab(prefabLevelArt);

		//CameraRig con la Main Camera ortográfica dentro
		var rig = new GameObject("CameraRig");
		rig.transform.rotation = Quaternion.Euler(40, 60, 0);
		var camGo = new GameObject("Main Camera");
		camGo.tag = "MainCamera";
		camGo.transform.SetParent(rig.transform, false);
		camGo.transform.localPosition = new Vector3(0, 0, -65);
		var cam = camGo.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = 15f;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(80f / 255f, 60f / 255f, 50f / 255f);
		cam.nearClipPlane = 0.3f;
		cam.farClipPlane = 200f;
		camGo.AddComponent<AudioListener>();
		var control = rig.AddComponent<CameraControl>();

		//EventSystem con los ejes de UI de la guía
		var eventos = new GameObject("EventSystem");
		eventos.AddComponent<EventSystem>();
		var modulo = eventos.AddComponent<StandaloneInputModule>();
		modulo.horizontalAxis = "HorizontalUI";
		modulo.verticalAxis = "VerticalUI";

		//SpawnPoints (icono azul y rojo)
		var spawn1 = new GameObject("SpawnPoint1");
		spawn1.transform.position = new Vector3(-3, 0, 30);
		spawn1.transform.rotation = Quaternion.Euler(0, 180, 0);
		EditorGUIUtility.SetIconForObject(spawn1, (Texture2D)EditorGUIUtility.IconContent("sv_label_1").image);
		var spawn2 = new GameObject("SpawnPoint2");
		spawn2.transform.position = new Vector3(13, 0, -5);
		spawn2.transform.rotation = Quaternion.Euler(0, 0, 0);
		EditorGUIUtility.SetIconForObject(spawn2, (Texture2D)EditorGUIUtility.IconContent("sv_label_6").image);

		//MessageCanvas con el texto ¡TANQUES!
		var mensajes = CanvasPantalla("MessageCanvas", 0);
		var texto = Texto(mensajes.transform, "Text", "¡TANQUES!", 60, TextAnchor.MiddleCenter,
			new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
		texto.resizeTextForBestFit = true;
		texto.resizeTextMinSize = 10;
		texto.resizeTextMaxSize = 60;

		//HUD de la actividad: nombre y matrícula, temporizador, marcador y pantalla final
		var hud = CanvasPantalla("HUDCanvas", 1);
		Texto(hud.transform, "NombreMatricula", NOMBRE + "\nMatrícula: " + MATRICULA, 30, TextAnchor.UpperRight,
			Vector2.one, Vector2.one, Vector2.one, new Vector2(-24, -18), new Vector2(700, 90));
		var temporizador = Texto(hud.transform, "Temporizador", "TIEMPO 4:00", 46, TextAnchor.UpperCenter,
			new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(500, 70));
		var marcador = Texto(hud.transform, "Marcador", "", 28, TextAnchor.UpperLeft,
			new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -18), new Vector2(700, 140));

		var panel = new GameObject("PantallaFinal", typeof(RectTransform));
		panel.transform.SetParent(hud.transform, false);
		Estirar(panel.GetComponent<RectTransform>());
		panel.AddComponent<Image>().color = new Color(0.08f, 0.05f, 0.04f, 0.82f);
		panel.transform.SetAsFirstSibling(); //el nombre, el tiempo y el marcador se siguen viendo encima
		var titulo = Texto(panel.transform, "Titulo", "", 80, TextAnchor.MiddleCenter,
			new Vector2(0.08f, 0.6f), new Vector2(0.92f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
		titulo.resizeTextForBestFit = true;
		titulo.resizeTextMinSize = 20;
		titulo.resizeTextMaxSize = 90;
		var detalle = Texto(panel.transform, "Detalle", "", 38, TextAnchor.UpperCenter,
			new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.58f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

		var boton = new GameObject("BotonJugarDeNuevo", typeof(RectTransform));
		boton.transform.SetParent(panel.transform, false);
		var botonRt = boton.GetComponent<RectTransform>();
		botonRt.anchorMin = botonRt.anchorMax = new Vector2(0.5f, 0.1f);
		botonRt.sizeDelta = new Vector2(420, 90);
		var botonImagen = boton.AddComponent<Image>();
		botonImagen.color = new Color(0.75f, 0.3f, 0.15f);
		var botonUi = boton.AddComponent<Button>();
		botonUi.targetGraphic = botonImagen;
		var botonTexto = Texto(boton.transform, "Text", "JUGAR DE NUEVO", 36, TextAnchor.MiddleCenter,
			Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

		//Actividad 3: música de fondo (marcha fúnebre) desde que inicia el juego
		var musica = new GameObject("MusicaFondo");
		Audio(musica, clipMarcha, true, true, 0.55f);

		//Actividad 7: enemigos repartidos por el mapa
		var enemigosPadre = new GameObject("Enemigos").transform;
		var posiciones = new[] { new Vector3(-20, 0, 8), new Vector3(25, 0, 12), new Vector3(5, 0, -22), new Vector3(6, 0, 12) };
		var enemigos = new Enemigo[posiciones.Length];
		for (int i = 0; i < posiciones.Length; i++) {
			var e = (GameObject)PrefabUtility.InstantiatePrefab(prefabEnemigo, enemigosPadre);
			e.name = "Enemigo" + (i + 1);
			e.transform.position = posiciones[i];
			e.transform.rotation = Quaternion.Euler(0, i * 90f, 0);
			enemigos[i] = e.GetComponent<Enemigo>();
		}

		//GameManager con sus referencias
		var gmGo = new GameObject("GameManager");
		var gm = gmGo.AddComponent<GameManager>();
		gm.m_CameraControl = control;
		gm.m_MessageText = texto;
		gm.m_TankPrefab = prefabTank;
		gm.m_Tanks = new[] {
			new TankManager { m_PlayerColor = new Color(42f / 255f, 100f / 255f, 178f / 255f), m_SpawnPoint = spawn1.transform },
			new TankManager { m_PlayerColor = new Color(229f / 255f, 46f / 255f, 40f / 255f), m_SpawnPoint = spawn2.transform },
		};
		gm.m_TimerText = temporizador;
		gm.m_ScoreText = marcador;
		gm.m_EndPanel = panel;
		gm.m_EndTitleText = titulo;
		gm.m_EndDetailText = detalle;
		gm.m_Enemigos = enemigos;
		UnityEventTools.AddPersistentListener(botonUi.onClick, gm.Restart);

		panel.SetActive(false);

		EditorSceneManager.SaveScene(escena, "Assets/Scenes/Principal.unity");
	}
}
