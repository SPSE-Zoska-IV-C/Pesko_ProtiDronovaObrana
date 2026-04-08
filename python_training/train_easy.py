import gym
import numpy as np
from pathlib import Path
from stable_baselines3 import SAC, PPO
from stable_baselines3.common.vec_env import SubprocVecEnv, DummyVecEnv, VecMonitor
from stable_baselines3.common.callbacks import CheckpointCallback, BaseCallback

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

# ========== SETUP FOLDERS ==========
print("Setting up project folders...")

PROJECT_ROOT = Path(__file__).parent.parent.resolve()

MODELS_DIR = PROJECT_ROOT / "models"
MODELS_EASY_DIR = MODELS_DIR / "easy"
MODELS_MEDIUM_DIR = MODELS_DIR / "medium"
MODELS_HARD_DIR = MODELS_DIR / "hard"
TB_LOGS_DIR = PROJECT_ROOT / "tb_logs"

MODELS_DIR.mkdir(exist_ok=True)
MODELS_EASY_DIR.mkdir(exist_ok=True)
MODELS_MEDIUM_DIR.mkdir(exist_ok=True)
MODELS_HARD_DIR.mkdir(exist_ok=True)
TB_LOGS_DIR.mkdir(exist_ok=True)

print(f"✓ Created: {MODELS_DIR}")
print(f"✓ Created: {MODELS_EASY_DIR}")
print(f"✓ Created: {TB_LOGS_DIR}\n")

# ==========================================

class UnityGymEnv(gym.Env):
    """Wraps Unity ML-Agents environment as a Gym environment"""
    
    def __init__(self, env_path, worker_id=0, time_scale=20):
        super().__init__()
        
        channel = EngineConfigurationChannel()
        channel.set_configuration_parameters(time_scale=time_scale)
        
        print(f"[Worker {worker_id}] Launching Unity...")
        
        self.unity_env = UnityEnvironment(
            file_name=str(env_path),
            worker_id=worker_id,
            no_graphics=True,
            side_channels=[channel],
            timeout_wait=120
        )
        
        self.unity_env.reset()
        self.behavior_name = list(self.unity_env.behavior_specs.keys())[0]
        spec = self.unity_env.behavior_specs[self.behavior_name]
        
        obs_shape = spec.observation_specs[0].shape[0]
        self.observation_space = gym.spaces.Box(
            low=-np.inf, 
            high=np.inf, 
            shape=(obs_shape,), 
            dtype=np.float32
        )
        
        if spec.action_spec.is_continuous():
            action_size = spec.action_spec.continuous_size
            self.action_space = gym.spaces.Box(
                low=-1.0, 
                high=1.0, 
                shape=(action_size,), 
                dtype=np.float32
            )
        
        # NEW: Track hits per episode
        self.episode_hits = 0
        self.previous_reward = 0
        
        print(f"[Worker {worker_id}] ✓ Ready (obs={obs_shape}, actions={action_size})")
    
    def reset(self):
        self.unity_env.reset()
        decision_steps, _ = self.unity_env.get_steps(self.behavior_name)
        obs = decision_steps.obs[0][0]
        
        # Reset hit counter
        self.episode_hits = 0
        self.previous_reward = 0
        
        return obs
    
    def step(self, action):
        from mlagents_envs.base_env import ActionTuple
        action_tuple = ActionTuple(continuous=np.array([action], dtype=np.float32))
        self.unity_env.set_actions(self.behavior_name, action_tuple)
        self.unity_env.step()
        
        decision_steps, terminal_steps = self.unity_env.get_steps(self.behavior_name)
        
        if len(terminal_steps) > 0:
            obs = terminal_steps.obs[0][0]
            reward = terminal_steps.reward[0]
            done = True
        else:
            obs = decision_steps.obs[0][0]
            reward = decision_steps.reward[0]
            done = False
        
        # NEW: Detect hits by reward spike
        # Hit reward is +5.0, so if we see a reward increase >= 4.0, it's likely a hit
        reward_delta = reward - self.previous_reward
        if reward_delta >= 4.0:
            self.episode_hits += 1
        
        self.previous_reward = reward
        
        # Add hit count to info
        info = {'hits': self.episode_hits}
        
        return obs, reward, done, info
    
    def close(self):
        if hasattr(self, 'unity_env'):
            self.unity_env.close()


class LoggingCallback(BaseCallback):
    """Custom callback for logging episode stats with hit tracking"""
    
    def __init__(self, verbose=0):
        super().__init__(verbose)
        self.episode_count = 0
        self.episode_rewards = []
        self.total_hits = 0  # NEW: Track total hits across all episodes
        self.episode_hits_list = []  # NEW: Track hits per episode
    
    def _on_step(self):
        if 'infos' in self.locals:
            for info in self.locals['infos']:
                if 'episode' in info:
                    ep_reward = info['episode']['r']
                    ep_length = info['episode']['l']
                    self.episode_count += 1
                    self.episode_rewards.append(ep_reward)
                    
                    # NEW: Extract hit count from info
                    ep_hits = info.get('hits', 0)
                    self.total_hits += ep_hits
                    self.episode_hits_list.append(ep_hits)
                    
                    mean_reward = np.mean(self.episode_rewards[-10:]) if len(self.episode_rewards) >= 10 else np.mean(self.episode_rewards)
                    mean_hits = np.mean(self.episode_hits_list[-10:]) if len(self.episode_hits_list) >= 10 else np.mean(self.episode_hits_list)
                    
                    # NEW: Show hits in output
                    print(f"\nEpisode {self.episode_count:4d} | Reward: {ep_reward:7.2f} | Length: {ep_length:4d} | Hits: {ep_hits:2d} | Mean(10): R={mean_reward:7.2f} H={mean_hits:4.1f} | Total Hits: {self.total_hits}")
        
        return True


def make_env(env_path, worker_id, time_scale=20):
    """Factory function for creating Unity environments"""
    def _init():
        return UnityGymEnv(str(env_path), worker_id=worker_id, time_scale=time_scale)
    return _init


if __name__ == "__main__":
    
    # ========== CONFIGURATION ==========
    ENV_PATH = PROJECT_ROOT / "EXPORTS" / "Easy_Preschool" / "Proti-dronova_Obrana.exe"
    
    N_ENVS = 12
    USE_SUBPROC = False
    TIME_SCALE = 20
    TOTAL_TIMESTEPS = 2_500_000
    ALGO = "ppo"
    LEARNING_RATE = 3e-4
    
    # ===================================
    
    print(f"Project root: {PROJECT_ROOT}")
    print(f"Exe path: {ENV_PATH}")
    print(f"Exists: {ENV_PATH.exists()}\n")
    
    if not ENV_PATH.exists():
        raise FileNotFoundError(f"Unity exe not found: {ENV_PATH}")
    
    print(f"Creating {N_ENVS} Unity environment(s)...")
    print(f"Mode: {'SubprocVecEnv (parallel)' if USE_SUBPROC else 'DummyVecEnv (single-process)'}")
    print("This may take 30-60 seconds...\n")
    
    try:
        env_fns = [make_env(ENV_PATH, worker_id=i, time_scale=TIME_SCALE) for i in range(N_ENVS)]
        
        if USE_SUBPROC and N_ENVS > 1:
            env = SubprocVecEnv(env_fns)
        else:
            env = DummyVecEnv(env_fns)
        
        env = VecMonitor(env)
        
        print(f"\n✓ All {N_ENVS} environment(s) ready!\n")
        
    except Exception as e:
        print(f"\n❌ Error creating environments: {e}")
        raise
    
    import torch
    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"Using device: {device}")
    if device == "cuda":
        print(f"GPU: {torch.cuda.get_device_name(0)}")
        
    # Create model
    print(f"Creating {ALGO.upper()} model...")
    if ALGO == "sac":
        model = SAC(
            "MlpPolicy",
            env,
            learning_rate=LEARNING_RATE,
            buffer_size=1_000_000,
            batch_size=256,
            verbose=1,
            tensorboard_log=str(TB_LOGS_DIR),
            device=device
        )
    else:
        model = PPO(
            "MlpPolicy",
            env,
            learning_rate=LEARNING_RATE,
            n_steps=2048,
            batch_size=64,
            verbose=1,
            tensorboard_log=str(TB_LOGS_DIR),
            device=device
        )
    
    # Callbacks
    checkpoint_cb = CheckpointCallback(
        save_freq=50_000,
        save_path=str(MODELS_EASY_DIR),
        name_prefix=f"{ALGO}_easy"
    )
    
    logging_cb = LoggingCallback()
    
    # Train
    print(f"\n🚀 Starting training ({TOTAL_TIMESTEPS:,} steps with {N_ENVS} env(s))...")
    print("="*80)
    print()
    
    try:
        model.learn(
            total_timesteps=TOTAL_TIMESTEPS,
            callback=[checkpoint_cb, logging_cb],
            progress_bar=True
        )
        
        final_path = MODELS_EASY_DIR / f"{ALGO}_easy_final"
        model.save(str(final_path))
        print(f"\n{'='*80}")
        print(f"✅ Training complete!")
        print(f"   Model: {final_path}.zip")
        print(f"   Total Hits: {logging_cb.total_hits}")
        print(f"   Total Episodes: {logging_cb.episode_count}")
        print(f"   Avg Hits/Episode: {logging_cb.total_hits / logging_cb.episode_count:.2f}")
        print(f"   Logs: tensorboard --logdir {TB_LOGS_DIR}")
        
    except KeyboardInterrupt:
        print(f"\n{'='*80}")
        print("⏹️  Training interrupted")
        interrupted_path = MODELS_EASY_DIR / f"{ALGO}_easy_interrupted"
        model.save(str(interrupted_path))
        print(f"   Model saved: {interrupted_path}.zip")
    
    finally:
        env.close()
        print("\nEnvironments closed")
