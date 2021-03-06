﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ShairportSharp.Helpers;

namespace ShairportSharp.Audio
{
    class AudioServer
    {
        const int DEFAULT_PORT = 6000;
        // Sockets
        object socketLock = new object();
        UdpClient audioSocket;
        UdpListener audioListener;

        // client address
        IPAddress clientAddress;

        // Audio infos and datas
        AudioSession audioSession;
        AudioBuffer audioBuffer;

        public AudioServer(AudioSession audioSession, int port)
        {            
            this.audioSession = audioSession;
            this.port = port.CheckValidPortNumber(DEFAULT_PORT, 3);
            audioBuffer = new DecryptedAudioBuffer(audioSession);
            audioBuffer.MissingPackets += requestResend;
        }

        int port = DEFAULT_PORT;
        public int Port
        {
            get { return port; }
        }

        internal AudioBuffer Buffer
        {
            get { return audioBuffer; }
        }

        public AudioBufferStream GetStream(StreamType streamType)
        {
            if (streamType == StreamType.Wave)
                return new WaveStream(audioBuffer, audioSession, false);
            return new AudioBufferStream(audioBuffer, audioSession);
        }

        /// <summary>
        /// Opens the sockets and begins listening for packets
        /// </summary>
        /// <returns>True if setup successful</returns>
        public bool Start()
        {
            lock (socketLock)
            {
                int tries = 0;
                bool result = false;
                while (tries < 10 && port < ushort.MaxValue - 1)
                {
                    try
                    {
                        audioSocket = new UdpClient(port);
                    }
                    catch (SocketException)
                    {
                        port++;
                        tries++;
                        continue;
                    }
                    result = true;
                    break;
                }

                if (result)
                {
                    Logger.Debug("Audio Server: Using port {0}", port);
                    audioListener = new UdpListener(audioSocket);
                    audioListener.OnPacketReceived += packetReceived;
                    audioListener.Start();
                }
                else
                {
                    Logger.Error("Audio Server: Failed to locate an available port");
                    if (audioSocket != null)
                    {
                        audioSocket.Close();
                        audioSocket = null;
                    }
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Closes all sockets and stops listening for new packets
        /// </summary>
        public void Stop()
        {
            lock (socketLock)
            {
                audioBuffer.Stop();
                if (audioListener != null)
                {
                    audioListener.Stop();
                    audioListener = null;
                }
                if (audioSocket != null)
                {
                    audioSocket.Close();
                    audioSocket = null;
                }
            }
        }

        /// <summary>
        /// Handles new packet
        /// </summary>
        /// <param name="socket">UdpCLient that received packet</param>
        /// <param name="packet">Packet data</param>
        /// <param name="remoteEndPoint">Packet source</param>        
        void packetReceived(UdpClient socket, byte[] packet, IPEndPoint remoteEndPoint)
        {
            this.clientAddress = remoteEndPoint.Address; //The client address
            int type = packet[1] & ~0x80;
            if (type == 0x60 || type == 0x56)
            { 	// audio data / resend
                // additional 4 bytes
                int offset = 0;
                if (type == 0x56)
                    offset = 4;
                //seqno is on two byte
                ushort seqno = (ushort)packet.IntFromBigEndian(2 + offset, 2);
                //next 12 bytes are rtp header
                int dataLength = packet.Length - offset - 12;

                //Logger.Debug("Audio data: {0} bytes", dataLength);
                //Video apps sometimes start an AirTunes session before sending video,
                //they send small packets of empty data so we try and ignore them here
                if (dataLength > 32)
                {
                    uint timeStamp = packet.UIntFromBigEndian(4 + offset, 4);
                    byte[] pktp = new byte[dataLength];
                    for (int i = 0; i < pktp.Length; i++)
                    {
                        //audio data
                        pktp[i] = packet[i + 12 + offset];
                    }
                    audioBuffer.PutPacketInBuffer(seqno, timeStamp, pktp);
                }
            }
            //else if (type == 0x54)
            //{
            //    //sync packet
            //    int packetLength = packet.Length - 12;
            //    if (packetLength > 0)
            //    {
            //        Logger.Debug("Got sync packet");
            //    }
            //}
        }


        /// <summary>
        /// Request resend of packets
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        void requestResend(object sender, MissingPacketEventArgs e)
        {
            if (e.Last < e.First)
                return;

            int len = e.Last - e.First;
            byte[] request = new byte[] { (byte)0x80, (byte)(0x55 | 0x80), 0x01, 0x00, (byte)((e.First & 0xFF00) >> 8), (byte)(e.First & 0xFF), (byte)((len & 0xFF00) >> 8), (byte)(len & 0xFF) };

            lock (socketLock)
            {
                if (audioSocket != null)
                {
                    try
                    {
                        audioSocket.Send(request, request.Length, clientAddress.ToString(), audioSession.ControlPort);
                    }
                    catch (SocketException) { }
                }
            }
        }
    }
}
