using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public int m_NumRoundsToWin = 5; //Número de rondas que un jugador debe ganar para ganar el juego
    public float m_StartDelay = 3f; //Delay entre las fases de RoundStarting y RoundPlaying
    public float m_EndDelay = 3f;  //Delay entre las fases de RoundPlaying y RoundEnding
    public CameraControl m_CameraControl; //Referencia al script de CameraControl
    public Text m_MessageText; //Referencia al texto para mostrar mensajes

    public GameObject m_TankPrefab; //Referencia al Prefab del Tanque
    public TankManager[] m_Tanks; //Array de TankManagers para controlar cada tanque

    [Header("Actividad")]
    public int m_MaxLosses = 5; //Actividad 4: el juego termina cuando un jugador pierde 5 veces
    public float m_MaxGameTime = 240f; //Actividad 6: el juego dura como máximo 4 minutos
    public Text m_TimerText; //Texto del temporizador (arriba en el centro)
    public Text m_ScoreText; //Marcador de victorias y derrotas (arriba a la izquierda)
    public GameObject m_EndPanel; //Actividad 5: pantalla final con el ganador
    public Text m_EndTitleText; //Título de la pantalla final
    public Text m_EndDetailText; //Puntaje y tiempo del ganador
    public Enemigo[] m_Enemigos; //Actividad 7: enemigos que obstaculizan a los tanques


    private int m_RoundNumber;  //Número de ronda
    private WaitForSeconds m_StartWait; //Delay hasta que la ronda empieza
    private WaitForSeconds m_EndWait; //Delay hasta que la ronda acaba
    private TankManager m_RoundWinner; //Referencia al ganador de la ronda para anunciar quién ha ganado
    private TankManager m_GameWinner; //Referencia al ganador del juego para anunciar quién ha ganado
    private float m_GameTime; //Tiempo que lleva la partida
    private bool m_GameOver; //True cuando ya se mostró la pantalla final
    private float m_GameOverTime; //Momento en que se mostró la pantalla final


    private void Start()
    {
        //Creamos los delays para que solo se apliquen una vez
        m_StartWait = new WaitForSeconds(m_StartDelay);
        m_EndWait = new WaitForSeconds(m_EndDelay);

        m_EndPanel.SetActive(false);

        SpawnAllTanks(); //Generar tanques
        SetCameraTargets(); //Ajustar cámara
        SetEnemyTargets(); //Los enemigos persiguen a los tanques

        UpdateHud();
        StartCoroutine(GameLoop()); //Iniciar juego
    }


    private void Update()
    {
        if (m_GameOver)
        {
            //En la pantalla final, R o Enter vuelven a empezar (con un pequeño margen para no saltarla sin querer)
            if (Time.time - m_GameOverTime > 1.5f &&
                (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                Restart();
            return;
        }

        //Actividad 6: temporizador de 4 minutos
        m_GameTime += Time.deltaTime;
        UpdateHud();

        //Si se acaba el tiempo y nadie ha ganado, ambos jugadores pierden
        if (m_GameTime >= m_MaxGameTime)
        {
            m_GameTime = m_MaxGameTime;
            StopAllCoroutines();
            DisableTankControl();
            DisableEnemies();
            ShowEndScreen(null, true);
        }
    }


    private void SpawnAllTanks()
    {
        //Recorro los tanques...
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            //...los creo, ajusto el número de jugador y las referencias necesarias para controlarlo
            m_Tanks[i].m_Instance =
                Instantiate(m_TankPrefab, m_Tanks[i].m_SpawnPoint.position, m_Tanks[i].m_SpawnPoint.rotation) as GameObject;
            m_Tanks[i].m_PlayerNumber = i + 1;
            m_Tanks[i].Setup();
        }
    }


    private void SetCameraTargets()
    {
        //Creo un array de Transforms del mismo tamaño que el número de tanques
        Transform[] targets = new Transform[m_Tanks.Length];

        //Recorro los Transforms...
        for (int i = 0; i < targets.Length; i++)
        {
            //...lo ajusto al transform del tanque apropiado
            targets[i] = m_Tanks[i].m_Instance.transform;
        }

        //Estos son los targets que la cámara debe seguir
        m_CameraControl.m_Targets = targets;
    }


    private void SetEnemyTargets()
    {
        for (int i = 0; i < m_Enemigos.Length; i++)
        {
            m_Enemigos[i].m_Targets = m_CameraControl.m_Targets;
        }
    }


    //llamado al principio y en cada fase del juego después de otra
    private IEnumerator GameLoop()
    {
        //Empiezo con la corutina RoundStarting y no retorno hasta que finalice
        yield return StartCoroutine(RoundStarting());

        //Cuando finalice RoundStarting, empiezo con RoundPlaying y no retorno hasta que finalice
        yield return StartCoroutine(RoundPlaying());

        //Cuando finalice RoundPlaying, empiezo con RoundEnding y no retorno hasta que finalice
        yield return StartCoroutine(RoundEnding());

        //Si ya hay un ganador (o alguien llegó a 5 derrotas)...
        if (m_GameWinner != null || SomeoneReachedMaxLosses())
        {
            //Actividad 5: en vez de reiniciar el nivel muestro la pantalla del ganador
            ShowEndScreen(m_GameWinner, false);
        }
        else
        {
            //Si no, reinicio las corutinas para que continúe el bucle
            //En este caso sin yield, de modo que esta versión del GameLoop finalizará siempre
            StartCoroutine(GameLoop());
        }
    }


    private IEnumerator RoundStarting()
    {
        // Cuando empiece la ronda reseteo los tanques e impido que se muevan.
        ResetAllTanks ();
        DisableTankControl ();
        ResetEnemies ();

        // Ajusto la cámara a los tanques reseteados.
        m_CameraControl.SetStartPositionAndSize ();

        // Incremento la ronda y muestro el texto informativo.
        m_RoundNumber++;
        m_MessageText.text = "ROUND " + m_RoundNumber;

        // Espero a que pase el tiempo de espera antes de volver al bucle.
        yield return m_StartWait;
    }


    private IEnumerator RoundPlaying()
    {
        // Cuando empiece la ronda dejo que los tanques se muevan.
        EnableTankControl ();
        EnableEnemies ();

        // Borro el texto de la pantalla.
        m_MessageText.text = string.Empty;

        // Mientras haya más de un tanque...
        while (!OneTankLeft())
        {
            // ... vuelvo al frame siguiente.
            yield return null;
        }
    }


    private IEnumerator RoundEnding()
    {
        // Deshabilito el movimiento de los tanques.
        DisableTankControl ();
        DisableEnemies ();

        // Borro al ganador de la ronda anterior.
        m_RoundWinner = null;

        // Miro si hay un ganador de la ronda.
        m_RoundWinner = GetRoundWinner ();

        // Si lo hay, incremento su puntuación.
        if (m_RoundWinner != null)
            m_RoundWinner.m_Wins++;

        // Actividad 4: cada tanque destruido suma una derrota a su jugador.
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            if (!m_Tanks[i].m_Instance.activeSelf)
                m_Tanks[i].m_Losses++;
        }

        // Compruebo si alguien ha ganado el juego.
        m_GameWinner = GetGameWinner ();

        // Genero el mensaje según si hay un ganador del juego o no.
        string message = EndMessage ();
        m_MessageText.text = message;
        UpdateHud ();

        // Espero a que pase el tiempo de espera antes de volver al bucle.
        yield return m_EndWait;
    }


    // Usado para comprobar si queda más de un tanque.
    private bool OneTankLeft()
    {
        // Contador de tanques.
        int numTanksLeft = 0;

        // recorro los tanques...
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            // ... si está activo, incremento el contador.
            if (m_Tanks[i].m_Instance.activeSelf)
                numTanksLeft++;
        }

        // Devuelvo true si queda 1 o menos, false si queda más de uno.
        return numTanksLeft <= 1;
    }


    // Comprueba si algún tanque ha ganado la ronda (si queda un tanque o menos).
    private TankManager GetRoundWinner()
    {
        // Recorro los tanques...
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            // ... si solo queda uno, es el ganador y lo devuelvo.
            if (m_Tanks[i].m_Instance.activeSelf)
                return m_Tanks[i];
        }

        // Si no hay ninguno activo es un empate, así que devuelvo null.
        return null;
    }


    private bool SomeoneReachedMaxLosses()
    {
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            if (m_Tanks[i].m_Losses >= m_MaxLosses)
                return true;
        }
        return false;
    }


    // Comprueba si hay algún ganador del juego.
    // Actividad 4: el juego termina cuando un jugador pierde 5 veces; gana el que tenga menos derrotas.
    private TankManager GetGameWinner()
    {
        if (!SomeoneReachedMaxLosses())
            return null;

        TankManager winner = null;
        bool tie = false;

        // Recorro los tanques buscando al que tenga menos derrotas...
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            if (winner == null || m_Tanks[i].m_Losses < winner.m_Losses)
            {
                winner = m_Tanks[i];
                tie = false;
            }
            else if (m_Tanks[i].m_Losses == winner.m_Losses)
            {
                tie = true;
            }
        }

        // Si los dos llegaron a 5 derrotas a la vez es empate, así que devuelvo null.
        return tie ? null : winner;
    }


    // Devuelve el texto del mensaje a mostrar al final de cada ronda.
    private string EndMessage()
    {
        // Por defecto no hay ganadores, así que es empate.
        string message = "EMPATE!";

        // Si hay un ganador de ronda cambio el mensaje.
        if (m_RoundWinner != null)
            message = m_RoundWinner.m_ColoredPlayerText + " GANA LA RONDA!";

        // Retornos de carro.
        message += "\n\n\n\n";

        // Recorro los tanques y añado sus puntuaciones.
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            message += m_Tanks[i].m_ColoredPlayerText + ": " + m_Tanks[i].m_Wins + " GANA - " + m_Tanks[i].m_Losses + " PIERDE\n";
        }

        // Si hay un ganador del juego, cambio el mensaje entero para reflejarlo.
        if (m_GameWinner != null)
            message = m_GameWinner.m_ColoredPlayerText + " GANA EL JUEGO!";

        return message;
    }


    // Para resetear los tanques (propiedades, posiciones, etc.).
    private void ResetAllTanks()
    {
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            m_Tanks[i].Reset();
        }
    }


    //Habilita el control del tanque
    private void EnableTankControl()
    {
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            m_Tanks[i].EnableControl();
        }
    }


    //Deshabilita el control del tanque
    private void DisableTankControl()
    {
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            m_Tanks[i].DisableControl();
        }
    }


    // ---------------------------------------------------------------- Actividad

    private void ResetEnemies()
    {
        for (int i = 0; i < m_Enemigos.Length; i++)
            m_Enemigos[i].ResetEnemy();
    }


    private void EnableEnemies()
    {
        for (int i = 0; i < m_Enemigos.Length; i++)
            m_Enemigos[i].m_Active = true;
    }


    private void DisableEnemies()
    {
        for (int i = 0; i < m_Enemigos.Length; i++)
            m_Enemigos[i].m_Active = false;
    }


    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return (total / 60) + ":" + (total % 60).ToString("00");
    }


    private void UpdateHud()
    {
        float remaining = Mathf.Max(0f, m_MaxGameTime - m_GameTime);
        m_TimerText.text = "TIEMPO " + FormatTime(Mathf.Ceil(remaining));
        m_TimerText.color = remaining <= 30f ? new Color(1f, 0.3f, 0.25f) : Color.white;

        string score = "";
        for (int i = 0; i < m_Tanks.Length; i++)
        {
            score += m_Tanks[i].m_ColoredPlayerText + "  " + m_Tanks[i].m_Wins + " V / " + m_Tanks[i].m_Losses + " D\n";
        }
        m_ScoreText.text = score + "(" + m_MaxLosses + " derrotas = fin)";
    }


    // Actividad 5 y 6: pantalla final con el ganador, su puntaje y el tiempo de la partida.
    private void ShowEndScreen(TankManager winner, bool timeUp)
    {
        m_GameOver = true;
        m_GameOverTime = Time.time;
        m_MessageText.text = string.Empty;
        UpdateHud();

        string detail = "";
        if (timeUp)
        {
            m_EndTitleText.text = "¡SE ACABÓ EL TIEMPO!\nAMBOS JUGADORES PIERDEN";
            detail = "Nadie terminó la partida en " + FormatTime(m_MaxGameTime) + " minutos.\n\n";
        }
        else if (winner != null)
        {
            m_EndTitleText.text = winner.m_ColoredPlayerText + " GANA EL JUEGO!";
            detail = "Puntaje del ganador: " + winner.m_Wins + " rondas ganadas\n"
                + "Tiempo en ganar la partida: " + FormatTime(m_GameTime) + "\n\n";
        }
        else
        {
            m_EndTitleText.text = "¡EMPATE!";
            detail = "Los dos jugadores llegaron a " + m_MaxLosses + " derrotas.\n"
                + "Tiempo de la partida: " + FormatTime(m_GameTime) + "\n\n";
        }

        for (int i = 0; i < m_Tanks.Length; i++)
        {
            detail += m_Tanks[i].m_ColoredPlayerText + ": " + m_Tanks[i].m_Wins + " victorias, " + m_Tanks[i].m_Losses + " derrotas\n";
        }

        m_EndDetailText.text = detail + "\nPresiona ENTER o R para jugar de nuevo";
        m_EndPanel.SetActive(true);
    }


    public void Restart()
    {
        SceneManager.LoadScene(0);
    }
}
