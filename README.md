Názov práce: Virtuálne prostedie pre posilňovacie učenie: Proti-dronová Ochrana

Úloha: Navrhnite virtuálne prostredie pre posilňovacie učenie (Reinforcement Learning - RL), ktoré bude zamerané na simuláciu protidronovej ochrany. Úlohou zadania je vytvoriť automatizovanú vežu proti dronom, teda agenta (telo pre RL model), ktorého funkciou bude zneškodňovať drony v danom prostredí.

Technické parametre:

    Programovanie v jazyku Python a C#.
    Programovanie vo VScode IDE.
    Práca v softvére Unity.
    Použité python knižnice: Stablebaseline3, ML-agents, Pytorch, Gymnasium



## 🚀 Inštalácia a spustenie

### Požiadavky
- Python 3.10+
- Unity 2022.3 LTS
- NVIDIA GPU (odporúčané)

### 1. Klonovanie repozitára
```bash
git clone https://github.com/SPSE-Zoska-IV-C/Pesko_ProtiDronovaObrana.git
cd Pesko_ProtiDronovaObrana
```

### 2. Inštalácia Python závislostí
```bash
pip install -r requirements.txt
```

### 3. Zostavenie Unity prostredia
1. Otvorte projekt v **Unity 2022.3 LTS**
2. Otvorte scénu `Assets/Scenes/AntiDroneEnv`
3. Zostavte projekt: `File → Build Settings → Build`
4. Uložte build do priečinka `/build`

### 4. Spustenie trénovania
```bash
python train.py --algo ppo --difficulty easy --timesteps 2500000
```

### 5. Testovanie natrénovaného modelu
```bash
python test.py --model models/best_model.zip
```

### 6. Vizualizácia výsledkov
```bash
tensorboard --logdir logs/
```
