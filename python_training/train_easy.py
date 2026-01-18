import gym
import numpy as np
from pathlib import Path
from stable_baselines3 import SAC, PPO
from stable_baselines3.common.vec_env import SubprocVecEnv, VecMonitor
from stable_baselines3.common.callbacks import CheckpointCallback, BaseCallback

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel


# ========== SETUP FOLDERS FIRST ==========
print("Setting up project folders...")

PROJECT_ROOT = Path(__file__).parent.parent.resolve()

# Create all necessary folders
MODELS_DIR = PROJECT_ROOT / "models"
MODELS_EASY_DIR = MODELS_DIR / "easy"
MODELS_MEDIUM_DIR = MODELS_DIR / "medium"
MODELS_HARD_DIR = MODELS_DIR / "hard"
TB_LOGS_DIR = PROJECT_ROOT / "tb_logs"

# Create folders
MODELS_DIR.mkdir(exist_ok=True)
MODELS_EASY_DIR.mkdir(exist_ok=True)
MODELS_MEDIUM_DIR.mkdir(exist_ok=True)
MODELS_HARD_DIR.mkdir(exist_ok=True)
TB_LOGS_DIR.mkdir(exist_ok=True)

print(f"✓ Created: {MODELS_DIR}")
print(f"✓ Created: {MODELS_EASY_DIR}")
print(f"✓ Created: {MODELS_MEDIUM_DIR}")
print(f"✓ Created: {MODELS_HARD_DIR}")
print(f"✓ Created: {TB_LOGS_DIR}")
print()

# ==========================================


class UnityGymEnv(gym.Env):
    """Wraps Unity ML-Agents environment as a Gym environment"""
    
    def __init__(self, env_path, worker_id=0, time_scale=20):
        super().__init__()
        
        channel = EngineConfigurationChannel()
        channel.set_configuration_parameters(time_scale=time_scale)
        
        self.unity_env = UnityEnvironment(
            file_name=str(env_path),
            worker_id=worker_id,
            no_graphics=True,
            side_channels=[channel]
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
        
        print(f"[Worker {worker_id}] ✓ Ready")
    
    def reset(self):
        self.unity_env.reset()
        decision_steps, _ = self.unity_env.get_steps(self.behavior_name)
        obs = decision_steps.obs[0][0]
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
        
        return obs, reward, done, {}
    
    def close(self):
        self.unity_env.close()


class LoggingCallback(BaseCallback):
    """Custom callback for logging episode stats"""
    
    def __init__(self, verbose=0):
        super().__init__(verbose)
        self.episode_count = 0
        self.episode_rewards = []
    
    def _on_step(self):
        if 'infos' in self.locals:
            for info in self.locals['infos']:
                if 'episode' in info:
                    ep_reward = info['episode']['r']
                    ep_length = info['episode']['l']
                    self.episode_count += 1
                    self.episode_rewards.append(ep_reward)
                    
                    mean_reward = np.mean(self.episode_rewards[-10:]) if len(self.episode_rewards) >= 10 else np.mean(self.episode_rewards)
                    
                    print(f"\nEpisode {self.episode_count:4d} | Reward: {ep_reward:8.2f} | Length: {ep_length:4d} | Mean(10): {mean_reward:8.2f}")
        
        return True


def make_env(env_path, worker_id, time_scale=20):
    """Factory function for creating Unity environments"""
    def _init():
        return UnityGymEnv(str(env_path), worker_id=worker_id, time_scale=time_scale)
    return _init


if __name__ == "__main__":
    
    # ========== CONFIGURATION ==========
    ENV_PATH = PROJECT_ROOT / "EXPORTS" / "EASY_v2" / "Proti-dronova_Obrana.exe"
    
    N_ENVS = 12             # Number of parallel Unity instances
    TIME_SCALE = 20         # Unity time scale (speedup)
    TOTAL_TIMESTEPS = 2_500_000
    ALGO = "ppo"            # "sac" or "ppo"
    LEARNING_RATE = 3e-4
    
    # ===================================
    
    print(f"Project root: {PROJECT_ROOT}")
    print(f"Exe path: {ENV_PATH}")
    print(f"Exists: {ENV_PATH.exists()}")
    print(f"\nModels will be saved to: {MODELS_EASY_DIR}")
    print(f"TensorBoard logs will be saved to: {TB_LOGS_DIR}\n")
    
    if not ENV_PATH.exists():
        raise FileNotFoundError(f"Unity exe not found: {ENV_PATH}")
    
    # Create parallel environments
    print(f"Creating {N_ENVS} parallel Unity environments...")
    print("This will launch multiple Unity instances (may take 30-60 seconds)...\n")
    
    env_fns = [make_env(ENV_PATH, worker_id=i, time_scale=TIME_SCALE) for i in range(N_ENVS)]
    env = SubprocVecEnv(env_fns)
    env = VecMonitor(env)
    
    print(f"\n✓ All {N_ENVS} environments ready!\n")
    
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
            tensorboard_log=str(TB_LOGS_DIR)
        )
    else:  # ppo
        model = PPO(
            "MlpPolicy",
            env,
            learning_rate=LEARNING_RATE,
            n_steps=2048,
            batch_size=64,
            verbose=1,
            tensorboard_log=str(TB_LOGS_DIR)
        )
    
    # Callbacks
    checkpoint_cb = CheckpointCallback(
        save_freq=50_000,
        save_path=str(MODELS_EASY_DIR),
        name_prefix=f"{ALGO}_easy"
    )
    
    logging_cb = LoggingCallback()
    
    # Train
    print(f"\n🚀 Starting training ({TOTAL_TIMESTEPS:,} steps with {N_ENVS} parallel envs)...")
    print("="*80)
    print()
    
    try:
        model.learn(
            total_timesteps=TOTAL_TIMESTEPS,
            callback=[checkpoint_cb, logging_cb],
            progress_bar=True
        )
        
        # Save final model
        final_path = MODELS_EASY_DIR / f"{ALGO}_easy_final"
        model.save(str(final_path))
        print(f"\n{'='*80}")
        print(f"✅ Training complete!")
        print(f"   Final model: {final_path}.zip")
        print(f"   All checkpoints: {MODELS_EASY_DIR}")
        print(f"   TensorBoard logs: {TB_LOGS_DIR}")
        print(f"\n   To view training stats:")
        print(f"   tensorboard --logdir {TB_LOGS_DIR}")
        
    except KeyboardInterrupt:
        print(f"\n{'='*80}")
        print("⏹️  Training interrupted by user")
        interrupted_path = MODELS_EASY_DIR / f"{ALGO}_easy_interrupted"
        model.save(str(interrupted_path))
        print(f"   Model saved: {interrupted_path}.zip")
    
    finally:
        env.close()
        print("\nEnvironments closed")
