import random
import numpy as np
import keras
import os
os.environ['TF_CPP_MIN_LOG_LEVEL'] = '3'

def reshape(valores, peEsquerdo):
    saida = np.empty((1, 7), float)[0]

    for i in range(3):
        saida[i] = valores[i] / 40

    index = 3
    for i in range(3):
        for ii in range(i + 1, 3):
            saida[index] = (saida[i] - saida[ii] + 1.0) * 0.5
            index += 1

    saida[index] = 1 if peEsquerdo else 0

    return saida


def main():
    modelo = keras.models.load_model('modelo_papete')
    layer_names = [layer.name for layer in modelo.layers]
    for layer_name in layer_names[:-1]:
        modelo.get_layer(layer_name).trainable = False
    entradas_treino = []
    saidas_treino = []
    while True:
        print("ok: ")
        entrada = input().split(";")
        if entrada[0] == "adicionar":
            entradas_treino.append(reshape([float(entrada[1]), float(entrada[2]), float(entrada[3])], entrada[4] == "E"))

            if entrada[5] == 'Dorsiflexao' or entrada[5] == '0':
                expected = [1, 0, 0, 0, 0]
            elif entrada[5] == 'Flexao' or entrada[5] == '1':
                expected = [0, 1, 0, 0, 0]
            elif entrada[5] == 'Repouso' or entrada[5] == '2':
                expected = [0, 0, 1, 0, 0]
            elif entrada[5] == 'Eversao' or entrada[5] == '3':
                expected = [0, 0, 0, 1, 0]
            elif entrada[5] == 'Inversao' or entrada[5] == '4':
                expected = [0, 0, 0, 0, 1]
            else:
                raise Exception("Movimento irregular: " + entrada[5])
            saidas_treino.append(expected)

        elif entrada[0] == "removerUltimo":
            entradas_treino = entradas_treino[:-1]
            saidas_treino = saidas_treino[:-1]

        elif entrada[0] == "limpar":
            entradas_treino = []
            saidas_treino = []

        elif entrada[0] == "retreinar":
            if len(entradas_treino) > 0:
                modelo.fit(np.array(entradas_treino), np.array(saidas_treino), epochs=110, batch_size=30, verbose=0)
                print(f"retreinado;1")
            else:
                print(f"retreinado;0")
            
        elif entrada[0] == "prever":
            peEsq = entrada[4] == "E"
            network_input = reshape([float(entrada[1]), float(entrada[2]), float(entrada[3])], peEsq).reshape(-1, 7)

            nn_output = modelo.predict(network_input,verbose=0)[0]

            somatorio = 0.0

            for i in range(5):
                somatorio += nn_output[i]

            for i in range(5):
                nn_output[i] /= somatorio
            esqs = "esq"
            dirs = "dir"
            print(f"{esqs if peEsq else dirs };{nn_output[0]};{nn_output[1]};{nn_output[2]};{nn_output[3]};{nn_output[4]}")

        elif entrada[0] == "sair":
            break
        else:
            print(f"comando não reconhecido:{entrada}")
            break
main()
