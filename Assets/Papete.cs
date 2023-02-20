using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;

public class Papete : MonoBehaviour
{
    public int debugData = 0;
    public static string[] NomesExterno = new string[] { "Dorsiflexão", "Flexão", "Repouso", "Eversão", "Inversão" };
    public static string[] NomesInterno = new string[] { "Dorsiflexao", "Flexao", "Repouso", "Eversao", "Inversao" };
    public enum Posicao
    {
        Eversao,
        Inversao,
        Repouso,
        Flexao,
        Dorsiflexao
    }
    public enum TipoDeConexao
    {
        Null,
        Udp,
        Serial
    }

    
    public UnityEvent<TipoDeConexao> alteracaoEstado;
    public bool permitirConexaoPorUdp = false;
    public GameObject calibPrefab;
    public UnityEvent<bool> eventoCalibracaoCompleta;
    
    private TMP_Text labelConexao;
    private Vector3 sensorEsquerdo;
    private Vector3 sensorDireito;
    private float ultimoRecebimentoEsquerdo;
    private float ultimoRecebimentoDireito;
    private TipoDeConexao conexaoPeEsquerdo = TipoDeConexao.Null;
    private TipoDeConexao conexaoPeDireito = TipoDeConexao.Null;
    private float maxEspera = 1f;

    private object mutexCalibracao = new object();
    private bool calibracaoConcluida = false;
    private bool resultadoUltimaCalibracao = true;

    
    //variaveis serial
    private ComunicArduino serial;

    //variaveis udp
    private string mensagemUdp;
    private object mutexUdp = new object();
    private bool dadoDisponivelUdp = false;
    private UdpClient udp;
    private Thread udpHandle;
    

    //variaveis interacao com python
    private object mutexCMD = new object();
    Queue<string> comandos = new();
    private bool pythonProntoParaProximoComando = false;

    private object mutexPrevisao = new object();
    private bool requisitouPrevisaoEsq = false;
    private bool requisitouPrevisaoDir = false;
    private float[] prevEsq = new float[] { 0f, 0f, 0f, 0f, 0f};
    private float[] prevDir = new float[] { 0f, 0f, 0f, 0f, 0f};

    //0 dir, 1 esq, 2 ambos
    Stack<int> dadosSalvos = new();
    int dadosSalvosEsq = 0;
    int dadosSalvosDir = 0;

    //Métodos Públicos
    #region Métodos Públicos
    public static bool TryParsePos(string s, out Posicao pos)
    {
        s = s.ToLower();
        for (int i = 0; i < 5; i++)
        {
            if (s == NomesInterno[i].ToLower())
            {
                pos = (Posicao)i;
                return true;
            }
            if (s == NomesExterno[i].ToLower())
            {
                pos = (Posicao)i;
                return true;
            }
        }

        pos = Posicao.Repouso;
        return false;
    }
    public static Vector2 ObterPosicaoCruz(Posicao pos, bool peEsquerdo)
    {
        if (pos == Posicao.Dorsiflexao)
            return new Vector2(0f, 1f);
        if (pos == Posicao.Flexao)
            return new Vector2(0f, -1f);
        if(peEsquerdo)
        if (pos == Posicao.Eversao)
            return new Vector2(-1f, 0f);
        return new Vector2(0f, 0f);
    }
    public Posicao ObterPosicao()
    {
        return ObterPosicao(PeEsquerdoEhMaisRecente());
    }
    public Posicao ObterPosicao(bool peEsquerdo)
    {
        float[] prev = ObterPrevisao(peEsquerdo);
        int maior = 0;
        for (int i = 1; i < 5; i++)
        {
            if (prev[i] > prev[maior])
                maior = i;
        }
        return (Posicao)maior;
    }
    public float[] ObterPrevisao()
    {
        return ObterPrevisao(PeEsquerdoEhMaisRecente());
    }
    public float[] ObterPrevisao(bool peEsquerdo)
    {
        float[] prev;
        lock (mutexPrevisao)
        {
            if (peEsquerdo)
            {
                if (!requisitouPrevisaoEsq)
                {
                    comandos.Enqueue("prever;" + sensorEsquerdo.x + ";" + sensorEsquerdo.y + ";" + sensorEsquerdo.z + ";E");
                    requisitouPrevisaoEsq = true;
                }
                prev = prevEsq;
            }
            else
            {
                if (!requisitouPrevisaoDir)
                {
                    comandos.Enqueue("prever;" + sensorDireito.x + ";" + sensorDireito.y + ";" + sensorDireito.z + ";D");
                    requisitouPrevisaoDir = true;
                }
                prev = prevDir;
            }
        }
        return prev;
    }
    public void AcionarColetaPadrao()
    {
        Limpar();
        Instantiate(calibPrefab).GetComponentInChildren<MenuCalibrador>().funcaoCancelar = RemoverObjetoCalibracaoPadrao;
    }
    public Vector3 ObterSensor(bool peEsquerdo)
    {
        return peEsquerdo? sensorEsquerdo:sensorDireito;
    }
    public Vector3 ObterRotacaoV3(bool peEsquerdo)
    {
        Vector2 v2 = ObterRotacaoV2(peEsquerdo);
        return new Vector3(v2.x, 0f, v2.y);
    }
    public Vector2 ObterRotacaoV2(bool peEsquerdo)
    {
        Vector3 sensor = peEsquerdo ? sensorEsquerdo : sensorDireito;

        float pitch = -(Mathf.Atan2(sensor.x, Mathf.Sqrt(sensor.y * sensor.y + sensor.z * sensor.z)) * 180.0f) / Mathf.PI;
        float roll = (Mathf.Atan2(sensor.y, sensor.z) * 180.0f) / Mathf.PI;

        //valores de correção descobertos manualmente, podem ser imprecisos
        if (peEsquerdo)
        {
            pitch += 3f;
            roll -= 81f;
            if (pitch < -30f)
                pitch += 54.2f;
            if (roll < -30f)
                roll += 52.5f;
        }
        else
        {
            pitch += 9f;
            roll -= 69f;
            if (pitch < -50f)
                pitch = -pitch - 57f;
            if (roll < -50f)
                roll += 76f;
        }
        return new Vector2(pitch, roll);
    }
    public bool PeEsquerdoEhMaisRecente()
    {
        print("pe esquerdo? " + (ultimoRecebimentoEsquerdo > ultimoRecebimentoDireito));
        return ultimoRecebimentoEsquerdo > ultimoRecebimentoDireito;
    }
    public bool SalvarDado(Posicao pos)
    {
        bool dir = conexaoPeDireito != TipoDeConexao.Null;
        bool esq = conexaoPeEsquerdo!= TipoDeConexao.Null;
        if (dir)
            lock (mutexCMD)
            {
                comandos.Enqueue("adicionar;" + sensorDireito.x + ";" + sensorDireito.y + ";" + sensorDireito.z + ";D;" + NomesInterno[(int)pos]);
            }
        if (esq)
            lock (mutexCMD)
            {
                comandos.Enqueue("adicionar;" + sensorEsquerdo.x + ";" + sensorEsquerdo.y + ";" + sensorEsquerdo.z + ";E;" + NomesInterno[(int)pos]);
            }
        if (esq && dir)
        {
            dadosSalvos.Push(2);
            dadosSalvosEsq++;
            dadosSalvosDir++;
            return true;
        }
        if (esq)
        {
            dadosSalvos.Push(1);
            dadosSalvosEsq++;
            return true;
        }
        if (dir)
        {
            dadosSalvos.Push(0);
            dadosSalvosDir++;
            return true;
        }
        return false;
    }
    public bool DesfazerUltimaColeta()
    {
        if(dadosSalvos.Count==0) return false;
        int top = dadosSalvos.Pop();
        lock (mutexCMD)
        {
            comandos.Enqueue("removerUltimo");
        }
        if (top == 0)
            dadosSalvosDir--;
        else if(top == 1) 
            dadosSalvosEsq--;
        else
        {
            lock (mutexCMD)
            {
                comandos.Enqueue("removerUltimo");
            }
            dadosSalvosEsq--;
            dadosSalvosDir--;
        }
        return true;
    }
    public void Limpar()
    {
        lock (mutexCMD)
        {
            comandos.Enqueue("limpar");
        }
        dadosSalvosDir = 0;
        dadosSalvosEsq = 0;
        dadosSalvos.Clear();
    }
    public void Calibrar()
    {
        lock (mutexCMD)
        {
            comandos.Enqueue("retreinar");
        }
        Limpar();
    }
    public TipoDeConexao ObterConexao(bool peEsquerdo)
    {
        if (peEsquerdo)
            return conexaoPeEsquerdo;
        return conexaoPeDireito;
    }
    #endregion

    #region Métodos privados

    private void ThreadInteracaoCMD()
    {
        var process = new Process();
        var psi = new ProcessStartInfo();
        psi.FileName = "cmd.exe";
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WorkingDirectory = Application.streamingAssetsPath + "\\python";
        process.StartInfo = psi;
        process.Start();
        process.OutputDataReceived += (sender, e) => { LidarComRespostaPython(e.Data); };
        process.ErrorDataReceived += (sender, e) => { print("ERRO: \""+e.Data+"\""); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using (StreamWriter sw = process.StandardInput)
        {
            pythonProntoParaProximoComando = false;
            sw.WriteLine("venv\\Scripts\\activate");
            sw.WriteLine("python main.py");
            bool rodando = true;
            while (rodando)
            {
                if (comandos.Count > 0 && pythonProntoParaProximoComando)
                {
                    //print("Enviando " + comandos.Peek());
                    lock (mutexCMD) {
                        pythonProntoParaProximoComando = false;
                        while(comandos.Count > 0)
                        {
                            string cmd = comandos.Dequeue();
                            if(cmd == "exit")
                                rodando = false;
                            sw.WriteLine(cmd);
                        }
                    }
                }
            }
        }
        process.WaitForExit();
    }

    private void LidarComRespostaPython(string resposta)
    {
        //print("resposta cmd: \""+resposta+"\"");

        string[] resposta_split = resposta.Split(';');
        if (resposta_split[0] == "ok: ")
        {
            pythonProntoParaProximoComando=true;
        }
        else if (resposta_split[0] == "retreinado")
        {
            print(resposta);
            lock (mutexCalibracao)
            {
                calibracaoConcluida = true;
                resultadoUltimaCalibracao = resposta_split[1].Contains("1");
            }
        }
        else if (resposta_split[0] == "esq")
        {
            lock (mutexPrevisao)
            {
                for (int i = 0; i < 5; i++)
                {
                    prevEsq[i] = float.Parse(resposta_split[i + 1]);
                }
                requisitouPrevisaoEsq = false;
            }
        }
        else if (resposta_split[0] == "dir")
        {
            lock (mutexPrevisao)
            {
                for (int i = 0; i < 5; i++)
                {
                    prevDir[i] = float.Parse(resposta_split[i + 1]);
                }
                requisitouPrevisaoDir = false;
            }
        }
    }

    private void RemoverObjetoCalibracaoPadrao()
    {
        if (FindObjectOfType<MenuCalibrador>())
        {
            Destroy(FindObjectOfType<MenuCalibrador>().transform.root.gameObject);
        }
    }

    private void InterpretarDadosRecebidos(string msg, TipoDeConexao conexao)
    {
        //Processar dados recebidos
        bool interpretados = false;
        string[] leituraLados = msg.Split(char.Parse("D"));
        for (int i = 0; i < leituraLados.Length; i++)
        {
            if (leituraLados[i].Length > 0)
            {

                bool peEsquerdo = leituraLados[i][0] == 'E';
                if (peEsquerdo)
                {
                    leituraLados[i] = leituraLados[i][1..];
                }

                string[] leitura = leituraLados[i].Split(char.Parse(";"));
                if (leitura.Length >= 3 &&
                    float.TryParse(leitura[0], out float x) &&
                    float.TryParse(leitura[1], out float y) &&
                    float.TryParse(leitura[2], out float z)
                    )
                {
                    if (peEsquerdo)
                    {
                        sensorEsquerdo.x = x;
                        sensorEsquerdo.y = y;
                        sensorEsquerdo.z = z;
                        ultimoRecebimentoEsquerdo = Time.time;
                        conexaoPeEsquerdo = conexao;
                    }
                    else
                    {
                        sensorDireito.x = x;
                        sensorDireito.y = y;
                        sensorDireito.z = z;
                        ultimoRecebimentoDireito = Time.time;
                        conexaoPeDireito = conexao;
                    }
                    interpretados = true;
                }

            }

        }
        if (!interpretados)
            print(msg);
    }

    // Comunicação Serial
    private void EventoSerial(string msg)
    {
        InterpretarDadosRecebidos(msg, TipoDeConexao.Serial);
    }
    private void EventoConexaoArduino(bool conexao) {
        if(!conexao) { serial.ConectarComPrimeiraPortaDisponivel(); }
    }

    //udp
    private void ThreadObservadoraUdp()
    {

        while (true)
        {
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

            byte[] receiveBytes = udp.Receive(ref RemoteIpEndPoint);

            /* Trava o objeto de controle para garantir que as variaveis
             * não estão sendo acessadas por mais de uma thread ao mesmo
             * tempo 
            */
            lock (mutexUdp)
            {
                mensagemUdp = Encoding.ASCII.GetString(receiveBytes);

                // levanta a bandeira para a thread principal que os dados estão prontos
                dadoDisponivelUdp = true;
            }
        }
    }

    #endregion

    #region Unity Messages
    //Unity Messages

    private void Start()
    {
        if (permitirConexaoPorUdp)
        {
            udp = new UdpClient(5555);
            udpHandle = new Thread(new ThreadStart(ThreadObservadoraUdp));
            udpHandle.Start();
        }
        
        new Thread(new ThreadStart(ThreadInteracaoCMD)).Start();


        labelConexao = GetComponentInChildren<TMP_Text>();
        serial = ComunicArduino.CriarReceptor(this.gameObject, EventoConexaoArduino, EventoSerial);
        serial.ConectarComPrimeiraPortaDisponivel();

    }

    private void Update()
    { 
        if (dadoDisponivelUdp)
        {
            lock (mutexUdp)
            {
                dadoDisponivelUdp = false;

                //Processar dados recebidos
                InterpretarDadosRecebidos(mensagemUdp,TipoDeConexao.Udp);
            }
        }
        lock(mutexCalibracao)
        {
            if (calibracaoConcluida)
            {
                calibracaoConcluida = false;
                RemoverObjetoCalibracaoPadrao();
                print("Resultado: " + resultadoUltimaCalibracao);
                eventoCalibracaoCompleta.Invoke(resultadoUltimaCalibracao);
            }
        }

        if (Time.time - ultimoRecebimentoDireito > maxEspera)
            conexaoPeDireito = TipoDeConexao.Null;
        if (Time.time - ultimoRecebimentoEsquerdo > maxEspera)
            conexaoPeEsquerdo= TipoDeConexao.Null;
        
        labelConexao.text = "";
        if (conexaoPeDireito == TipoDeConexao.Udp)
            labelConexao.text += "dir  <sprite index= 0>";
        else if (conexaoPeDireito == TipoDeConexao.Serial)
            labelConexao.text += "dir  <sprite index= 1>";
        if (conexaoPeEsquerdo == TipoDeConexao.Udp)
            labelConexao.text += "\nesq  <sprite index= 0>";
        else if (conexaoPeEsquerdo == TipoDeConexao.Serial)
            labelConexao.text += "\nesq  <sprite index= 1>";

    }

    private void OnDestroy()
    {
        if(udpHandle != null)
        {
            udpHandle.Abort();
            udp.Close();
        }
    }

    #endregion
}
