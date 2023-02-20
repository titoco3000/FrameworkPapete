/*
 * Permite a comunica��o do arduino com Unity, tanto em um sentido quanto no outro,
 * al�m da conex�o autom�tica entre eles. N�o depende do tipo do arduino ou da porta 
 * pela qual ele se comunica - �COM3�, �COM5�, etc. 
 * 
 * 22/06/2022
 * por Tito Guidotti
 * 
 */
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.IO.Ports; //para isso funcionar, API compatibility level n�o pode ser subset
using System.Threading;

public class ComunicArduino:MonoBehaviour
{
    public bool conectado;
    public int baudRate = 115200;

    public UnityEvent<bool> eventoConexao = new UnityEvent<bool>();
    public UnityEvent<string> eventoSerial = new UnityEvent<string>();
    
    private Thread thread;
    private SerialPort porta;


    //objeto que indica qual thread est� no controle das variaveis
    static readonly object cadeado = new object();

    //variaveis de comunica��o da thread de recebimento para a principal
    private string valorRecebido = "";
    private string buffer = "";
    private bool valorDisponivel = false;
    private bool forcarFechamento  = false;

    /// <summary>
    /// Cria um novo ComunicArduino dinamicamente
    /// </summary>
    /// <param name="pai">Onde vai ser criado</param>
    /// <param name="eventoConexao">Fun��o chamada quando o arduino � conectado/desconectado</param>
    /// <param name="eventoMensagemSerial">Fun��o chamada quando o arduino envia mensagens seriais (Serial.println)</param>
    /// <param name="baudRate">velocidade de comunica��o serial</param>
    /// <returns>A inst�ncia criada</returns>
    public static ComunicArduino CriarReceptor(GameObject pai, UnityAction<bool> eventoConexao, UnityAction<string> eventoMensagemSerial, int baudRate = 115200)
    {
        ComunicArduino serial = pai.AddComponent<ComunicArduino>();
        serial.eventoConexao.AddListener(eventoConexao);
        serial.eventoSerial.AddListener(eventoMensagemSerial);
        serial.baudRate = baudRate;
        return serial;
    }


    /// <summary>
    /// Retorna um vetor dos nomes das portas dispon�veis no sistema
    /// </summary>
    /// <returns>Vetor dos nomes das portas dispon�veis</returns>
    public string[] PortasDisponiveis()
    {
        return SerialPort.GetPortNames();
    }

    /// <summary>
    /// Tenta conectar na porta especificada
    /// </summary>
    /// <param name="nome">Nome da porta a ser conectada</param>
    /// <returns>Se conectou com sucesso ou n�o</returns>
    public bool ConectarPorta(string nome)
    {
        if (conectado)
            throw new System.Exception("Tentando conectar em mais de uma porta ao mesmo tempo");
        try
        {
            lock (cadeado)
            {
                porta = new SerialPort(nome, baudRate);
                porta.Open();
                thread = new Thread(new ThreadStart(ThreadObservadoraSerial));
                thread.Start();
                conectado = true;
                eventoConexao.Invoke(true);
            }
        }
        catch (System.Exception)
        {

        }
        return conectado;
    }

    /// <summary>
    /// Desconecta da porta atual
    /// </summary>
    /// <exception cref="System.Exception">Quando n�o est� conectado, d� uma exce��o</exception>
    public void DesconectarPortaAtual()
    {
        lock (cadeado){
            if (!conectado)
                throw new System.Exception("Tentando desconectar de uma porta inexistente");
            thread.Abort();
            porta.Close();
            conectado = false;
            eventoConexao.Invoke(false);
            forcarFechamento = false;
        }
    }

    /// <summary>
    /// Entra em um loop paralelo at� conseguir conectar com um arduino.
    /// N�o h� garantia de funcionamento se mais de um for conectado, e outros dispositivos podem causar problemas tamb�m (teoricamente)
    /// </summary>
    public void ConectarComPrimeiraPortaDisponivel()
    {
        //apenas para o caso de j� estar em execu��o
        StopCoroutine(ConectarComPrimeiroCasoCoroutine());
        StartCoroutine(ConectarComPrimeiroCasoCoroutine());
    }

    /// <summary>
    /// Envia uma mensagem serial para o arduino
    /// </summary>
    /// <param name="msg">A mensagem a ser enviada</param>
    /// <exception cref="System.Exception">Quando tenta enviar e n�o est� conectado</exception>
    public void Enviar(string msg)
    {
        if (!conectado)
            throw new System.Exception("N�o est� conectado");
        lock (cadeado)
        {
            porta.WriteLine(msg);
        }
    }
    /// <summary>
    /// Coroutine que busca o primeiro arduino conectado
    /// </summary>
    private IEnumerator ConectarComPrimeiroCasoCoroutine()
    {
        while (!conectado)
        {
            string[] portasDisponiveis = PortasDisponiveis();
            if (portasDisponiveis.Length > 0)
            {
                ConectarPorta(portasDisponiveis[0]);
            }
            yield return null;
        }
    }

    /// <summary>
    /// Thread secund�ria, fica em loop tentado ler a porta conectada e invoca o evento especificado
    /// </summary>
    private void ThreadObservadoraSerial()
    {
        while (true)
        {
            try
            {
                buffer = porta.ReadLine();
                lock (cadeado)
                {
                    valorRecebido = buffer;
                    valorDisponivel = true;
                }
            }
            //esse erro acontece quando o arduino � desconectado, portanto o sistema deve fechar a porta
            catch (System.IO.IOException)
            {
                forcarFechamento  = true;
            }
            
        }
    }

    private void Update()
    {
        if (forcarFechamento )
        {
            DesconectarPortaAtual();
        }
        if (valorDisponivel)
        {
            lock (cadeado)
            {
                eventoSerial.Invoke(valorRecebido);
                valorDisponivel = false;
            }
        }
    }
    /// <summary>
    /// Libera os recursos reservados
    /// </summary>
    private void OnDestroy()
    {
        if (conectado)
        {
            thread.Abort();
            porta.Close();
        }
    }

    
}
