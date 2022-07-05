﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Tuya virtual IR remote control
    /// </summary>
    public class TuyaIRControl : TuyaDevice
    {
        /// <summary>
        /// Creates a new instance of the TuyaDevice class.
        /// </summary>
        /// <param name="ip">IP address of device.</param>
        /// <param name="localKey">Local key of device (obtained via API).</param>
        /// <param name="deviceId">Device ID.</param>
        /// <param name="protocolVersion">Protocol version.</param>
        /// <param name="port">TCP port of device.</param>
        /// <param name="receiveTimeout">Receive timeout  (msec).</param>
        public TuyaIRControl(string ip, string localKey, string deviceId, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33, int port = 6668, int receiveTimeout = 250)
            : base(ip, localKey, deviceId, protocolVersion, port, receiveTimeout)
        {
        }

        /// <summary>
        /// Learns button code of remote control.
        /// </summary>
        /// <param name="timeout">Learing timeout, you should press RC button during this interval.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Button code as Base64 string.</returns>
        public async Task<string> GetButtonCodeAsync(int timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                var subCmd = new Dictionary<string, object>()
                {
                    { "control", "study_exit" }
                };
                var subCmdJson = JsonConvert.SerializeObject(subCmd);
                await SetDpsAsync(new Dictionary<int, object>() { { 201, subCmdJson } }, nullRetries: 0, allowEmptyResponse: true, cancellationToken: cancellationToken);

                await Task.Delay(1000);

                subCmd = new Dictionary<string, object>()
                {
                    { "control", "study" }
                };
                subCmdJson = JsonConvert.SerializeObject(subCmd);
                await SetDpsAsync(new Dictionary<int, object>() { { 201, subCmdJson } }, cancellationToken: cancellationToken);

                while (true)
                {
                    var response = await SetDpsAsync(new Dictionary<int, object>() { { 201, subCmdJson } }, overrideRecvTimeout: timeout, allowEmptyResponse: true, cancellationToken: cancellationToken);
                    if (response != null)
                    {
                        return response[202].ToString();
                    }
                }
            } finally
            {
                try
                {
                    var subCmd = new Dictionary<string, object>()
                    {
                        { "control", "study_exit" }
                    };
                    var subCmdJson = JsonConvert.SerializeObject(subCmd);
                    await SetDpsAsync(new Dictionary<int, object>() { { 201, subCmdJson } }, nullRetries: 0, allowEmptyResponse: true, cancellationToken: cancellationToken);
                }
                catch { }
            }
        }

        /// <summary>
        /// Sends button code.
        /// </summary>
        /// <param name="buttonCode">Button code in Base64 encoding.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendButtonCodeAsync(string buttonCode, CancellationToken cancellationToken = default)
        {
            var subCmd = new Dictionary<string, object>()
            {
                { "control", "send_ir" },
                { "key1", buttonCode }
            };
            var subCmdJson = JsonConvert.SerializeObject(subCmd);
            await SetDpsAsync(new Dictionary<int, object>() { { 201, subCmdJson } }, nullRetries: 0, allowEmptyResponse: true, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Converts Base64 encoded button code into pulses duration.
        /// </summary>
        /// <param name="codeBase64">Base64 encoded button code.</param>
        /// <returns>Pulses/gaps length in microsecods.</returns>
        public static ushort[] Base64ToPulses(string codeBase64)
        {
            var bytes = Convert.FromBase64String(codeBase64);
            var pulses = Enumerable.Range(2, bytes.Length - 3)
                .Where(x => x % 2 == 0)
                .Select(x => (ushort)((bytes[x] << 8 | bytes[x + 1]) / 4))
                .ToArray();
            return pulses;
        }

        /// <summary>
        /// Converts pulses duration into Base64 encoded button code.
        /// </summary>
        /// <param name="pulses">Pulses/gaps length in microsecods.</param>
        /// <returns>Base64 encoded button code.</returns>
        public static string PulsesToBase64(ushort[] pulses)
        {
            var pulsesMult = pulses.Select(x => (ushort)(x * 4)); // Convert 1 us to 250ns
            var code64 = Enumerable.Concat<UInt16>(new ushort[] { 0xd500 /* Uknown value*/ }, pulsesMult);
            var code64b = code64.SelectMany(x => new byte[] { (byte)((x >> 8) & 0xFF), (byte)(x & 0xFF) });
            code64b = Enumerable.Concat<byte>(code64b, new byte[] { 8 }); // Unknown padding byte
            var codeBase64 = Convert.ToBase64String(code64b.ToArray());
            return codeBase64;
        }

        /// <summary>
        /// Converts hex encoded button code into pulses duration.
        /// </summary>
        /// <param name="codeHex">Hex encoded button code.</param>
        /// <returns>Pulses/gaps length in microsecods.</returns>
        public static ushort[] HexToPulses(string codeHex)
        {
            var pulses = Enumerable.Range(0, codeHex.Length)
                .Where(x => (x % 4) == 0)
                .Select(x => (ushort)(Convert.ToUInt16(codeHex.Substring(x + 2, 2) + codeHex.Substring(x, 2), 16)));
            return pulses.ToArray();
        }

        /// <summary>
        /// Converts pulses duration into hex encoded button code.
        /// </summary>
        /// <param name="pulses">Pulses/gaps length in microsecods.</param>
        /// <returns>Hex encoded button code.</returns>
        public static string PulsesToHex(ushort[] pulses)
        {
            var words = Enumerable.Range(0, pulses.Length)
                .Select(x => $"{(pulses[x] & 0xFF):x02}{((pulses[x] >> 8) & 0xFF):x02}").ToArray();
            var hex = string.Concat(words);
            return hex;
        }
    }
}
