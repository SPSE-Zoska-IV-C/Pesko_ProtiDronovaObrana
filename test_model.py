import numpy as np
# Patch deprecated numpy alias
if not hasattr(np, 'bool'):
    np.bool = bool
if not hasattr(np, 'int'):
    np.int = int
if not hasattr(np, 'float'):
    np.float = float
if not hasattr(np, 'complex'):
    np.complex = complex
if not hasattr(np, 'object'):
    np.object = object
if not hasattr(np, 'str'):
    np.str = str

# Now import everything else
import gym
from pathlib import Path
from stable_baselines3 import SAC, PPO
import time
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel



class UnityGymEnv(gym.Env):
    """Wraps Unity ML-Agents environment as a Gym environment"""
    
    def __init__(self, env_path, worker_id=0, time_scale=1):
        super().__init__()
        
        channel = EngineConfigurationChannel()
        channel.set_configuration_parameters(time_scale=time_scale)
        
        self.unity_env = UnityEnvironment(
            file_name=str(env_path),
            worker_id=worker_id,
            no_graphics=False,  # Show graphics for testing
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


if __name__ == "__main__":
    # Use absolute path directly
    PROJECT_ROOT = Path("D:/Documents/My_projects/Unity/Pesko_ProtiDronovaObrana").resolve()
    
    # Or auto-detect based on where test_model.py is located
    # If test_model.py is in python_training folder:
    # PROJECT_ROOT = Path(__file__).parent.parent.resolve()
    
    # If test_model.py is in project root:
    # PROJECT_ROOT = Path(__file__).parent.resolve()
    
    # Path to your trained model
    # MODEL_PATH = PROJECT_ROOT / "models" / "easy" / "sac_easy_final.zip"
    MODEL_PATH = PROJECT_ROOT / "models" / "easy" / "ppo_easy_final.zip"
    
    # Path to Unity environment
    ENV_PATH = PROJECT_ROOT / "EXPORTS" / "EASY" / "Proti-dronova_Obrana.exe"
    
    print(f"Project root: {PROJECT_ROOT}")
    print(f"Model path: {MODEL_PATH}")
    print(f"Model exists: {MODEL_PATH.exists()}")
    print(f"Env path: {ENV_PATH}")
    print(f"Env exists: {ENV_PATH.exists()}\n")
    
    if not MODEL_PATH.exists():
        raise FileNotFoundError(f"Model not found: {MODEL_PATH}")
    
    if not ENV_PATH.exists():
        raise FileNotFoundError(f"Environment not found: {ENV_PATH}")

    
    # Load trained model
    # model = SAC.load(MODEL_PATH)
    model = PPO.load(MODEL_PATH)
    print("✓ Model loaded!\n")
    
    # Create environment (with graphics, normal speed)
    print("Creating Unity environment (with graphics)...")
    env = UnityGymEnv(str(ENV_PATH), worker_id=0, time_scale=1)
    
    # Run episodes
    num_episodes = 5
    
    for episode in range(1, num_episodes + 1):
        obs = env.reset()
        episode_reward = 0
        episode_length = 0
        done = False
        
        print(f"\n--- Episode {episode} ---")
        
        while not done:
            # Get action from trained model
            action, _states = model.predict(obs, deterministic=True)
            
            # Take step in environment
            obs, reward, done, info = env.step(action)
            
            episode_reward += reward
            episode_length += 1
            
            # Optional: slow down to watch
            # time.sleep(0.01)
        
        print(f"Episode {episode} finished!")
        print(f"  Total reward: {episode_reward:.2f}")
        print(f"  Length: {episode_length} steps")
    
    env.close()
    print("\n✅ Testing complete!")
