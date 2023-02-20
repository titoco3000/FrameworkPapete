using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.IO;
using System;
using UnityEngine.Events;
using Unity.VisualScripting;

public class NucleoJogo : MonoBehaviour
{
    private class Relatorio
    {
        private bool peEsquerdo;
        private List<Vector2> angulo;
        private List<Papete.Posicao> desejada;
        private List<Papete.Posicao> obtida;
        public Relatorio(bool peEsquerdo, int tamanhoEsperado)
        {
            this.peEsquerdo= peEsquerdo;
            angulo = new List<Vector2>();
            desejada = new List<Papete.Posicao>();
            obtida= new List<Papete.Posicao>();
        }

        public void Adicionar(Vector2 angulo, Papete.Posicao desejada, Papete.Posicao obtida)
        {
            this.angulo.Add(angulo);
            this.desejada.Add(desejada);
            this.obtida.Add(obtida);
        }

        public string ToCSV()
        {
            string s = "Frontal;Lateral;obtida;desejada;" + (peEsquerdo ? "E" : "D");
            for (int i = 0; i < angulo.Count; i++)
                s += "\n" + angulo[i].x.ToString("n2") + ";" + angulo[i].y.ToString("n2") + ";" + Papete.NomesInterno[(int)obtida[i]] + ";" + Papete.NomesInterno[(int)desejada[i]];
            return s;
        }
    }
    public Papete papete;
    public UnityEvent<float> EventoFimDeJogo;
    public float intervaloMedicaoRelatorio = 0.5f;

    private bool peEsquerdo;
    private float tempoDeInicio;

    private uint momentoSequenciaAtual = 0;
    private float marco = 0f;
    private float pontuacao = 0f;
    private bool jogando = false;
    private float ultimaMedicaoRelatorio;
    private Relatorio relatorio;
    private Papete.Posicao[] sequenciaPosicao;
    private uint[] sequenciaTempo;
    

    private uint tempoTotal;

    private Vector2 posVisualJogador = new Vector2();
    private float velocidadeVisualJogador = 10f;

    #region métodos públicos
    public void IniciarExercicio(string arquivo, bool peEsquerdo)
    {
        string[] linhas = arquivo.Split('\n');
        sequenciaPosicao = new Papete.Posicao[linhas.Length];
        sequenciaTempo = new uint[linhas.Length];
        relatorio = new Relatorio(peEsquerdo, linhas.Length);

        tempoTotal= 0;

        uint lidos = 0;
        for (int i = 0; i < linhas.Length; i++)
        {
            string[] palavras = linhas[i].Split(';');
            if (palavras.Length > 1)
            {
                if (Papete.TryParsePos(palavras[0], out Papete.Posicao pos) && uint.TryParse(palavras[1],out uint tempo)){
                    sequenciaPosicao[lidos] = pos;
                    sequenciaTempo[lidos] = tempo;
                    tempoTotal += tempo;
                    lidos++;
                }

            }
        }
        this.peEsquerdo = peEsquerdo;
        tempoDeInicio = Time.time;
        ultimaMedicaoRelatorio = tempoDeInicio;
        momentoSequenciaAtual = 0;
        marco = 0f;
        pontuacao = 0f;
        jogando = true;
    }

    /*
     Considerando as posicoes como uma cruz:
       E
     F R D
       I
    Em qualquer momento, acertar a posição vale 1.0; um pro lado, 0.5; uma diagonal, 0.25; o oposto, 0.0
     */
    public float ObterPontuacaoInstantanea()
    {
        Papete.Posicao prev = papete.ObterPosicao(peEsquerdo);
        if (sequenciaPosicao[momentoSequenciaAtual] == prev)
            return 1f;

        if (sequenciaPosicao[momentoSequenciaAtual] == Papete.Posicao.Dorsiflexao)
        {
            if (prev == Papete.Posicao.Repouso)
                return 0.5f;
            if (prev == Papete.Posicao.Flexao)
                return 0f;
        }
        if (sequenciaPosicao[momentoSequenciaAtual] == Papete.Posicao.Flexao)
        {
            if (prev == Papete.Posicao.Repouso)
                return 0.5f;
            if (prev == Papete.Posicao.Dorsiflexao)
                return 0f;
        }
        if (sequenciaPosicao[momentoSequenciaAtual] == Papete.Posicao.Eversao)
        {
            if (prev == Papete.Posicao.Repouso)
                return 0.5f;
            if (prev == Papete.Posicao.Inversao)
                return 0f;
        }
        if (sequenciaPosicao[momentoSequenciaAtual] == Papete.Posicao.Inversao)
        {
            if (prev == Papete.Posicao.Repouso)
                return 0.5f;
            if (prev == Papete.Posicao.Eversao)
                return 0f;
        }
        return 0.25f;
    }
    
    public Papete.Posicao ObterPosicaoDesejadaEm(float progresso)
    {
        uint busca = (uint)(progresso * tempoTotal);
        for (int i = 0; i < sequenciaTempo.Length; i++)
        {
            if (sequenciaTempo[i]>= busca)
            {
                return sequenciaPosicao[i];
            }
        }
        return Papete.Posicao.Repouso;
    }

    public float ObterPontuacaoAtual()
    {
        return pontuacao;
    }

    public Papete.Posicao ObterPosicaoDesejadaAgora()
    {
        if (jogando)
            return sequenciaPosicao[momentoSequenciaAtual];
        return Papete.Posicao.Repouso;
    }

    public Papete.Posicao ObterPosicaoJogador()
    {
        return papete.ObterPosicao(peEsquerdo);
    }
    public string ObterRelatorio()
    {
        if (relatorio == null)
            return "";
        return relatorio.ToCSV();
    }
    #endregion
    #region UnityMessages

    void Update()
    {
        if (jogando)
        {
            if (momentoSequenciaAtual < sequenciaPosicao.Length-1 )
            {
                if(Time.time - tempoDeInicio > marco + sequenciaTempo[momentoSequenciaAtual])
                    marco += sequenciaTempo[momentoSequenciaAtual++];
                pontuacao += Time.deltaTime * ObterPontuacaoInstantanea();
            
                if(Time.time - ultimaMedicaoRelatorio > intervaloMedicaoRelatorio)
                {
                    relatorio.Adicionar(papete.ObterRotacaoV2(peEsquerdo), ObterPosicaoDesejadaAgora(), ObterPosicaoJogador());
                    ultimaMedicaoRelatorio = Time.time;
                }
                posVisualJogador = Vector2.Lerp(posVisualJogador, Papete.ObterPosicaoCruz(papete.ObterPosicao(peEsquerdo),peEsquerdo), Time.deltaTime * velocidadeVisualJogador);
            }
            //bloco fim de jogo
            else
            {
                jogando= false;
                EventoFimDeJogo.Invoke(pontuacao);
            }
        }
    }
    #endregion
}
