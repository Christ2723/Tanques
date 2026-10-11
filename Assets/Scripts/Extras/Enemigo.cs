using UnityEngine;

//Actividad 7: tanque enemigo que obstaculiza a los jugadores mientras pelean.
//Patrulla por su zona, persigue al tanque más cercano, le dispara bombas y le hace daño si lo choca.
//Los jugadores lo pueden destruir con sus bombas; reaparece al rato en su zona.
public class Enemigo : MonoBehaviour
{
    public float m_Speed = 5f; //Velocidad de movimiento
    public float m_TurnSpeed = 120f; //Velocidad de giro en grados por segundo
    public float m_DetectionRange = 16f; //Distancia a la que ve a un tanque
    public float m_StopDistance = 7f; //Distancia a la que deja de acercarse para disparar
    public float m_PatrolRadius = 9f; //Radio de la zona que patrulla
    public float m_StartingHealth = 60f; //Salud del enemigo
    public float m_ContactDamage = 15f; //Daño al chocar con un tanque
    public float m_ContactCooldown = 1f; //Tiempo entre daños por choque
    public float m_FireRate = 3f; //Segundos entre disparos
    public float m_ShellDamage = 25f; //Daño máximo de sus bombas (menos que el de los jugadores)
    public float m_RespawnTime = 8f; //Segundos hasta que reaparece al ser destruido
    public Rigidbody m_Shell; //Prefab de la bomba (el mismo que usan los tanques)
    public Transform m_FireTransform; //Desde dónde dispara
    public AudioSource m_ShootingAudio; //Fuente de audio del disparo
    public AudioClip m_FireClip; //Sonido del disparo
    public GameObject m_ExplosionPrefab; //Explosión al ser destruido
    public LayerMask m_ObstacleMask; //Capas que cuentan como obstáculos del escenario

    [HideInInspector] public Transform[] m_Targets; //Tanques de los jugadores (los asigna el GameManager)
    [HideInInspector] public bool m_Active; //El GameManager lo activa solo mientras se juega la ronda

    private Rigidbody m_Rigidbody;
    private Collider m_Collider;
    private Renderer[] m_Renderers;
    private Color[] m_BaseColors;
    private ParticleSystem m_ExplosionParticles;
    private AudioSource m_ExplosionAudio;
    private Vector3 m_Home;
    private Quaternion m_HomeRotation;
    private Vector3 m_PatrolPoint;
    private float m_NextPatrolChange;
    private float m_Health;
    private float m_NextFire;
    private float m_NextContact;
    private float m_RespawnAt;
    private float m_Flash;
    private bool m_Dead;


    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Collider = GetComponent<Collider>();
        m_Renderers = GetComponentsInChildren<Renderer>();
        m_BaseColors = new Color[m_Renderers.Length];
        for (int i = 0; i < m_Renderers.Length; i++)
            m_BaseColors[i] = m_Renderers[i].material.color;

        m_Home = transform.position;
        m_HomeRotation = transform.rotation;

        m_ExplosionParticles = Instantiate(m_ExplosionPrefab).GetComponent<ParticleSystem>();
        m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource>();
        m_ExplosionParticles.gameObject.SetActive(false);

        ResetEnemy();
    }


    //Lo llama el GameManager al empezar cada ronda
    public void ResetEnemy()
    {
        m_Active = false;
        Respawn();
    }


    private void Respawn()
    {
        m_Dead = false;
        m_Health = m_StartingHealth;
        m_Flash = 0f;
        transform.position = m_Home;
        transform.rotation = m_HomeRotation;
        m_Rigidbody.position = m_Home;
        m_Rigidbody.rotation = m_HomeRotation;
        m_Collider.enabled = true;
        SetVisible(true);
        PickPatrolPoint();
        m_NextFire = Time.time + m_FireRate;
    }


    private void SetVisible(bool visible)
    {
        for (int i = 0; i < m_Renderers.Length; i++)
            m_Renderers[i].enabled = visible;
    }


    private void PickPatrolPoint()
    {
        Vector2 random = Random.insideUnitCircle * m_PatrolRadius;
        m_PatrolPoint = m_Home + new Vector3(random.x, 0f, random.y);
        m_NextPatrolChange = Time.time + Random.Range(3f, 6f);
    }


    private void Update()
    {
        //Si está destruido, reaparece pasado un tiempo (solo durante la ronda)
        if (m_Dead)
        {
            if (m_Active && Time.time >= m_RespawnAt)
                Respawn();
            return;
        }

        //Parpadeo blanco al recibir daño
        if (m_Flash > 0f)
        {
            m_Flash = Mathf.Max(0f, m_Flash - Time.deltaTime * 4f);
            for (int i = 0; i < m_Renderers.Length; i++)
                m_Renderers[i].material.color = Color.Lerp(m_BaseColors[i], Color.white, m_Flash);
        }
    }


    private void FixedUpdate()
    {
        //Quieto (cinemático) mientras no se juega o si está destruido
        bool frozen = !m_Active || m_Dead;
        if (m_Rigidbody.isKinematic != frozen)
            m_Rigidbody.isKinematic = frozen;
        if (frozen)
            return;

        Transform target = NearestTarget();
        bool chasing = target != null;

        Vector3 destination;
        if (chasing)
        {
            destination = target.position;
        }
        else
        {
            Vector3 toPatrol = m_PatrolPoint - m_Rigidbody.position;
            toPatrol.y = 0f;
            if (toPatrol.magnitude < 1.5f || Time.time >= m_NextPatrolChange)
                PickPatrolPoint();
            destination = m_PatrolPoint;
        }

        Vector3 direction = destination - m_Rigidbody.position;
        direction.y = 0f;
        float distance = direction.magnitude;
        if (distance < 0.01f)
            return;

        //Giro hacia el destino
        Quaternion look = Quaternion.LookRotation(direction);
        m_Rigidbody.MoveRotation(Quaternion.RotateTowards(m_Rigidbody.rotation, look, m_TurnSpeed * Time.deltaTime));
        float angle = Quaternion.Angle(m_Rigidbody.rotation, look);

        //Avanzo si no he llegado y estoy mirando más o menos hacia allí
        bool wantsToMove = chasing ? distance > m_StopDistance : distance > 1f;
        if (wantsToMove && angle < 45f)
        {
            Vector3 origin = m_Rigidbody.position + Vector3.up * 0.8f;
            bool blocked = Physics.Raycast(origin, transform.forward, 2.5f, m_ObstacleMask, QueryTriggerInteraction.Ignore);

            if (blocked)
            {
                //Si hay un obstáculo delante, rodeo girando (o busco otro punto de patrulla)
                if (!chasing)
                    PickPatrolPoint();
                m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.Euler(0f, m_TurnSpeed * Time.deltaTime, 0f));
            }
            else
            {
                m_Rigidbody.MovePosition(m_Rigidbody.position + transform.forward * m_Speed * Time.deltaTime);
            }
        }

        //Disparo al tanque si lo tengo delante
        if (chasing && angle < 15f && Time.time >= m_NextFire)
            Fire(distance);
    }


    //Busca el tanque activo más cercano dentro del rango de detección
    private Transform NearestTarget()
    {
        if (m_Targets == null)
            return null;

        Transform nearest = null;
        float best = m_DetectionRange;
        for (int i = 0; i < m_Targets.Length; i++)
        {
            if (m_Targets[i] == null || !m_Targets[i].gameObject.activeSelf)
                continue;

            float d = Vector3.Distance(m_Targets[i].position, m_Rigidbody.position);
            if (d < best)
            {
                best = d;
                nearest = m_Targets[i];
            }
        }
        return nearest;
    }


    private void Fire(float distance)
    {
        m_NextFire = Time.time + m_FireRate;

        //Fuerza aproximada para que la bomba caiga cerca del objetivo
        float force = Mathf.Clamp(Mathf.Sqrt(distance / 0.0349f) * 0.9f, 10f, 25f);

        Rigidbody shellInstance = Instantiate(m_Shell, m_FireTransform.position, m_FireTransform.rotation);
        shellInstance.linearVelocity = force * m_FireTransform.forward;
        shellInstance.GetComponent<ShellExplosion>().m_MaxDamage = m_ShellDamage;

        m_ShootingAudio.clip = m_FireClip;
        m_ShootingAudio.Play();
    }


    //Si choca con un tanque le hace daño
    private void OnCollisionStay(Collision collision)
    {
        if (!m_Active || m_Dead || Time.time < m_NextContact)
            return;

        TankHealth tank = collision.collider.GetComponent<TankHealth>();
        if (tank == null)
            return;

        m_NextContact = Time.time + m_ContactCooldown;
        tank.TakeDamage(m_ContactDamage);
    }


    //Lo llama ShellExplosion cuando una bomba explota cerca
    public void TakeDamage(float amount)
    {
        if (m_Dead || amount <= 0f)
            return;

        m_Health -= amount;
        m_Flash = 1f;

        if (m_Health <= 0f)
            Die();
    }


    private void Die()
    {
        m_Dead = true;
        m_RespawnAt = Time.time + m_RespawnTime;

        m_ExplosionParticles.transform.position = transform.position;
        m_ExplosionParticles.gameObject.SetActive(true);
        m_ExplosionParticles.Play();
        m_ExplosionAudio.Play();

        for (int i = 0; i < m_Renderers.Length; i++)
            m_Renderers[i].material.color = m_BaseColors[i];
        SetVisible(false);
        m_Collider.enabled = false;
        m_Rigidbody.isKinematic = true;
    }
}
