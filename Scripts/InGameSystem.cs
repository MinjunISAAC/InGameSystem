using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InGameSystem
{
    /// <summary>
    /// Core in-game loop manager using a StateMachine pattern.
    ///
    /// Setup:
    ///   1. Attach to a persistent GameObject in the game scene.
    ///   2. Assign WaveData list via SetWaveTable() before calling Init().
    ///   3. Subscribe to events (OnChangeWave, OnChangeCoin, etc.) from UI controllers.
    ///
    /// State flow:
    ///   Ready → Play ⇄ Pause
    ///                ⇄ Upgrade
    ///              → Win | Fail
    /// </summary>
    public class InGameSystem : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────

        public EInGameState CurrState { get; private set; } = EInGameState.Unknown;
        public EInGameState PrevState { get; private set; } = EInGameState.Unknown;

        // ── Wave ──────────────────────────────────────────────────────────────

        public int  CurrWave      { get; private set; } = 0;
        public int  MaxWave       { get; private set; } = 0;
        public float PlayTime     { get; private set; } = 0f;
        public float PlayTimeScale{ get; private set; } = 1f;

        private float _savedTimeScale = 1f;
        private List<WaveData> _waveTable = new();
        private Coroutine _coState        = null;
        private Coroutine _coSpawnEnemy   = null;
        private bool _isSpawnCompleted    = false;
        private bool _isGameStart         = false;

        // ── Coin ──────────────────────────────────────────────────────────────

        public int Coin { get; private set; } = 100;

        // ── Events (subscribe from UI — no direct coupling) ───────────────────

        public event Action<EInGameState> OnStateChanged   = null;
        public event Action<int>          OnChangeWave     = null;
        public event Action<int>          OnChangeCoin     = null;
        public event Action<float>        OnChangeTimeScale= null;
        public event Action<int>          OnEnemySpawned   = null; // enemyId
        public event Action               OnWaveCompleted  = null;

        // ── Public API ────────────────────────────────────────────────────────

        public void SetWaveTable(List<WaveData> table)
        {
            _waveTable = table;
            MaxWave    = table?.Count ?? 0;
        }

        public void Init(Action<EInGameState> onStateChanged = null)
        {
            PlayTime      = 0f;
            PlayTimeScale = 1f;
            CurrWave      = 0;
            _isGameStart  = false;

            if (onStateChanged != null)
                OnStateChanged += onStateChanged;

            ChangeState(EInGameState.Ready);
        }

        public void Reset()
        {
            StopAllCoroutines();
            _coState      = null;
            _coSpawnEnemy = null;
            PlayTime      = 0f;
            CurrWave      = 0;
            Coin          = 100;
            CurrState     = EInGameState.Unknown;
            PrevState     = EInGameState.Unknown;
            _isGameStart  = false;
        }

        public void ChangeState(EInGameState state,
                                Action<EInGameState> onStart = null,
                                Action<EInGameState> onDone  = null)
        {
            if (_coState != null)
                StopCoroutine(_coState);

            if (CurrState == state) return;

            PrevState = CurrState;
            CurrState = state;
            OnStateChanged?.Invoke(state);

            _coState = state switch
            {
                EInGameState.Ready   => StartCoroutine(Co_Ready(state, onStart, onDone)),
                EInGameState.Play    => StartCoroutine(Co_Play(state, onStart, onDone)),
                EInGameState.Pause   => StartCoroutine(Co_Pause(state, onStart, onDone)),
                EInGameState.Upgrade => StartCoroutine(Co_Upgrade(state, onStart, onDone)),
                EInGameState.Win     => StartCoroutine(Co_Win(state, onStart, onDone)),
                EInGameState.Fail    => StartCoroutine(Co_Fail(state, onStart, onDone)),
                _                    => null,
            };
        }

        public void SetPlayTimeScale(float scale)
        {
            if (scale > 0f)
                _savedTimeScale = scale;

            PlayTimeScale = scale;
            OnChangeTimeScale?.Invoke(scale);
        }

        public void AddCoin(int amount)
        {
            Coin += amount;
            OnChangeCoin?.Invoke(Coin);
        }

        public bool SpendCoin(int cost)
        {
            if (Coin < cost) return false;
            Coin -= cost;
            OnChangeCoin?.Invoke(Coin);
            return true;
        }

        // ── Unity Update ──────────────────────────────────────────────────────

        private void Update()
        {
            if (CurrState != EInGameState.Play) return;

            PlayTime += Time.deltaTime * PlayTimeScale;
            CheckWaveAdvance();
        }

        // ── Wave ──────────────────────────────────────────────────────────────

        private void CheckWaveAdvance()
        {
            var nextWave = CurrWave + 1;
            if (nextWave > MaxWave) return;

            var nextData = GetWaveData(nextWave);
            if (nextData == null) return;

            if (PlayTime >= nextData.StartTime)
            {
                CurrWave = nextWave;
                OnChangeWave?.Invoke(CurrWave);
                StartWaveSpawn(CurrWave);
            }
        }

        private void StartWaveSpawn(int wave)
        {
            if (_coSpawnEnemy != null)
                StopCoroutine(_coSpawnEnemy);

            _isSpawnCompleted = wave >= MaxWave ? _isSpawnCompleted : false;
            _coSpawnEnemy = StartCoroutine(Co_SpawnEnemies(wave));
        }

        private IEnumerator Co_SpawnEnemies(int wave)
        {
            var data = GetWaveData(wave);
            if (data == null) yield break;

            float delay = data.SpawnDelay > 0f ? data.SpawnDelay : 0.5f;

            foreach (var enemyId in data.EnemyIds)
            {
                // Wait respects PlayTimeScale so pause/speedup affects spawning
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    elapsed += Time.deltaTime * PlayTimeScale;
                    yield return null;
                }

                OnEnemySpawned?.Invoke(enemyId);
            }

            if (wave >= MaxWave)
            {
                _isSpawnCompleted = true;
                OnWaveCompleted?.Invoke();
            }
        }

        private WaveData GetWaveData(int wave)
            => _waveTable.Find(d => d.Wave == wave);

        // ── State Coroutines ──────────────────────────────────────────────────

        private IEnumerator Co_Ready(EInGameState state,
                                     Action<EInGameState> onStart,
                                     Action<EInGameState> onDone)
        {
            onStart?.Invoke(state);
            yield return null;
            onDone?.Invoke(state);
        }

        private IEnumerator Co_Play(EInGameState state,
                                    Action<EInGameState> onStart,
                                    Action<EInGameState> onDone)
        {
            onStart?.Invoke(state);

            // Restore saved speed when returning from Pause / Upgrade
            if (PrevState == EInGameState.Pause || PrevState == EInGameState.Upgrade)
                SetPlayTimeScale(_savedTimeScale);

            if (!_isGameStart)
                _isGameStart = true;

            yield return null;
            onDone?.Invoke(state);
        }

        private IEnumerator Co_Pause(EInGameState state,
                                     Action<EInGameState> onStart,
                                     Action<EInGameState> onDone)
        {
            onStart?.Invoke(state);
            if (PlayTimeScale > 0f) _savedTimeScale = PlayTimeScale;
            SetPlayTimeScale(0f);
            yield return null;
            onDone?.Invoke(state);
        }

        private IEnumerator Co_Upgrade(EInGameState state,
                                       Action<EInGameState> onStart,
                                       Action<EInGameState> onDone)
        {
            onStart?.Invoke(state);
            if (PlayTimeScale > 0f) _savedTimeScale = PlayTimeScale;
            SetPlayTimeScale(0f);
            yield return null;
            onDone?.Invoke(state);
        }

        private IEnumerator Co_Win(EInGameState state,
                                   Action<EInGameState> onStart,
                                   Action<EInGameState> onDone)
        {
            onStart?.Invoke(state);
            SetPlayTimeScale(0f);
            yield return null;
            onDone?.Invoke(state);
        }

        private IEnumerator Co_Fail(EInGameState state,
                                    Action<EInGameState> onStart,
                                    Action<EInGameState> onDone)
        {
            onStart?.Invoke(state);
            SetPlayTimeScale(0f);
            yield return null;
            onDone?.Invoke(state);
        }
    }
}
