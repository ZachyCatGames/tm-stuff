import os

for i in range(230000000):
    try:
        os.remove(f'recv_{i}')
    except:
        pass

