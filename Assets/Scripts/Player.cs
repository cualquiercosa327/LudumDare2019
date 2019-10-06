﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [System.Serializable]
    public class EvolutionStage
    {
        public string Name;
        public int AttackDamage;
        public int EnergyUntilNext;
        public float ScaleMin;
        public float ScaleMax;
        public float MoveSpeedScale;
        public Sprite Avatar;
    }

    public Vector2 MoveSpeed = new Vector2(6.0f, 6.0f);
    public Vector2 Drag = new Vector2(10.0f, 10.0f);
    public Vector2 DeadZoneLeft = new Vector2(0.4f, 0.4f);
    public float DeadZoneRight = 0.1f;
    public float DeadZoneFire = 0.1f;
    public float Gravity = 9.8f;
    public float FireTime = 0.5f;
    public float StartTime = 2.0f;
    public GameObject BulletPrefab;
    public EnemySpawner EnemySpawner;
    public EvolutionStage[] EvolutionStages;

    public AudioClip[] SFX_Strain;
    public AudioClip[] SFX_Pop;
    public AudioClip[] SFX_Spit;

    public Vector3 Velocity
    {
        get { return m_velocity; }
    }

    public Vector2 StickInputLeft
    {
        get { return m_stickInputLeft; }
    }

    public Vector2 StickInputRight
    {
        get { return m_stickInputRight; }
    }

    public EvolutionStage CurrentEvolution
    {
        get { return EvolutionStages[m_currentStageIdx]; }
    }

    private Vector2 m_stickInputLeft;
    private Vector2 m_stickInputRight;
    private Vector2 m_initialMoveSpeed;
    private Vector3 m_velocity;
    private float m_fireTimer;
    private SpriteRenderer m_sprite;
    private CharacterController m_characterController;
    private Sucker m_sucker;
    private AudioSource m_audio;
    private int m_currentStageIdx;
    private int m_currentStageEnergy;
    private float m_startTimer;

    void Start()
    {
        m_sprite = GetComponentInChildren<SpriteRenderer>();
        m_characterController = GetComponent<CharacterController>();
        m_sucker = GetComponentInChildren<Sucker>();
        m_audio = GetComponent<AudioSource>();
        m_initialMoveSpeed = MoveSpeed;
        SetEvolutionStage(0);

        //Start invisible, play strain SFX
        m_sprite.enabled = false;
        m_startTimer = StartTime;
        m_audio.clip = SFX_Strain[(int)Random.Range(0, SFX_Strain.Length)];
        m_audio.Play();
    }

    void SetEvolutionStage(int index)
    {
        //Set stage index and reset energy counter
        m_currentStageIdx = index;
        m_currentStageEnergy = 0;

        //Set new avatar and scale
        EvolutionStage stage = CurrentEvolution;
        m_sprite.sprite = stage.Avatar;
        transform.localScale = new Vector3(stage.ScaleMin, stage.ScaleMin, stage.ScaleMin);

        //Set enemy spawner to match
        EnemySpawner.SetEvolutionStage(index);
    }

    void AddEnergy(int energy)
    {
        //Add to counter
        m_currentStageEnergy += energy;

        //Increase scale
        EvolutionStage stage = CurrentEvolution;
        float stageTime = (float)m_currentStageEnergy / (float)stage.EnergyUntilNext;
        float scale = Mathf.Lerp(stage.ScaleMin, stage.ScaleMax, stageTime);
        m_sprite.transform.localScale = new Vector3(scale, scale, scale);

        //If hit next evolution stage, switch
        if (m_currentStageEnergy >= EvolutionStages[m_currentStageIdx].EnergyUntilNext)
        {
            SetEvolutionStage(m_currentStageIdx + 1);
        }
    }

    void Update()
    {
        if (m_startTimer > 0.0f)
        {
            m_startTimer -= Time.deltaTime;
            if (m_startTimer <= 0.0f)
            {
                m_audio.clip = SFX_Pop[(int)Random.Range(0, SFX_Pop.Length)];
                m_audio.Play();
                m_sprite.enabled = true;
            }
        }
        else
        {
            //Grab input
            Vector2 stickInputLeft = new Vector2(Input.GetAxis("MoveX"), -Input.GetAxis("MoveY"));
            Vector2 stickInputRight = new Vector2(Input.GetAxisRaw("RotateX"), Input.GetAxisRaw("RotateY"));

            //Apply deadzone
            if (Mathf.Abs(stickInputLeft.x) < DeadZoneLeft.x)
                stickInputLeft.x = 0.0f;
            if (Mathf.Abs(stickInputLeft.y) < DeadZoneLeft.y)
                stickInputLeft.y = 0.0f;

            m_stickInputLeft = stickInputLeft;
            m_stickInputRight = stickInputRight;

            //Apply velocity (scaled by current evolution)
            m_velocity += new Vector3(m_stickInputLeft.x * CurrentEvolution.MoveSpeedScale, -Gravity * Time.deltaTime, m_stickInputLeft.y * CurrentEvolution.MoveSpeedScale);

            //Apply drag
            m_velocity.x /= 1.0f + Drag.x * Time.deltaTime;
            m_velocity.z /= 1.0f + Drag.y * Time.deltaTime;

            m_characterController.Move(Velocity * Time.deltaTime);

            //Zero Y velocity if on floor
            if ((m_characterController.collisionFlags & CollisionFlags.Below) != 0)
            {
                m_velocity.y = 0.0f;
            }

            //Calc rotation direction
            if (m_stickInputRight.SqrMagnitude() > DeadZoneRight)
            {
                Vector2 directionVector = m_stickInputRight.normalized;
                Quaternion directionQuat = Quaternion.FromToRotation(new Vector3(0.0f, 0.0f, -1.0f), new Vector3(-directionVector.x, 0.0f, directionVector.y));

                //Apply rotation
                transform.rotation = directionQuat;
            }

            //Fire spitballs
            m_fireTimer -= Time.deltaTime;

            if (m_fireTimer <= 0.0f && Input.GetAxis("Fire1") > DeadZoneFire)
            {
                m_fireTimer = FireTime;

                if (BulletPrefab)
                {
                    GameObject spitballObj = Instantiate(BulletPrefab, transform.position, transform.rotation) as GameObject;
                    Spitball spitball = spitballObj.GetComponent<Spitball>();
                    spitball.Damage = CurrentEvolution.AttackDamage;

                    if(!m_audio.isPlaying)
                    {
                        m_audio.clip = SFX_Spit[(int)Random.Range(0, SFX_Spit.Length)];
                        m_audio.Play();
                    }
                }
            }

            //Suck up goo
            if (Input.GetAxis("Fire2") > DeadZoneFire)
            {
                m_sucker.StartSuck();
            }
            else
            {
                m_sucker.EndSuck();
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Enemy")
        {
            Enemy enemy = collision.gameObject.GetComponent<Enemy>();

            //If gooified enemy, consume
            if(enemy.CurrentState == Enemy.State.Goo)
            {
                //Take energy
                AddEnergy(enemy.Energy);

                //Remove enemy
                Destroy(collision.gameObject);
            }
            else
            {
                //Not gooified, harm player
            }
        }
    }
}