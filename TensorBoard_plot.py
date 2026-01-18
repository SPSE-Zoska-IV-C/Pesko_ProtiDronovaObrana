import tensorflow as tf
import tensorboard
import os
import subprocess
import webbrowser

# Set the log directory (change this to your actual log directory)
log_dir = "logs/fit"

# Launch TensorBoard
def launch_tensorboard(log_dir):
    tb_command = ["tensorboard", "--logdir", log_dir]
    print(f"Launching TensorBoard at logdir: {log_dir}")
    subprocess.Popen(tb_command)
    webbrowser.open("http://localhost:6006")

if __name__ == "__main__":
    if not os.path.exists(log_dir):
        print(f"Log directory '{log_dir}' does not exist.")
    else:
        launch_tensorboard(log_dir)