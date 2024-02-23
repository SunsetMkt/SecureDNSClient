﻿using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using System.Net;
using MsmhToolsClass;
using System.Diagnostics;
using MsmhToolsClass.DnsTool;

namespace SecureDNSClient;

public class CamouflageDNSServer
{
    public string DohUrl { get; set; }
    public string DohCleanIP { get; set; }
    public bool IsRunning { get; private set; } = false;
    public int Port { get; set; } = 5380;
    private string DohHost { get; set; }
    private int DohPort { get; set; } = 443;
    private DnsServer? DNSServer;

    public CamouflageDNSServer(int port, string dohUrl, string dohCleanIP)
    {
        Port = port;
        DohUrl = dohUrl.Trim();
        DohCleanIP = dohCleanIP.Trim();
        NetworkTool.GetUrlDetails(dohUrl.Trim(), 443, out _, out string host, out _, out _, out int dohPort, out string _, out bool _);
        DohHost = host;
        DohPort = dohPort;
    }

    public void Start()
    {
        UdpServerTransport udpServerTransport = new(new IPEndPoint(IPAddress.Any, Port));
        TcpServerTransport tcpServerTransport = new(new IPEndPoint(IPAddress.Any, Port));
        IServerTransport[] serverTransports = new IServerTransport[] { udpServerTransport, tcpServerTransport };
        if (DNSServer == null)
        {
            try
            {
                DNSServer = new(serverTransports);
                DNSServer.QueryReceived -= DnsServer_QueryReceived;
                DNSServer.QueryReceived += DnsServer_QueryReceived;
                DNSServer.Start();
                IsRunning = true;
            }
            catch (Exception)
            {
                try
                {
                    DNSServer = new(udpServerTransport);
                    DNSServer.QueryReceived -= DnsServer_QueryReceived;
                    DNSServer.QueryReceived += DnsServer_QueryReceived;
                    DNSServer.Start();
                    IsRunning = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    IsRunning = false;
                }
            }
        }
    }

    public void Stop()
    {
        if (DNSServer != null)
        {
            DNSServer.Stop();
            IsRunning = false;
        }
    }

    private async Task DnsServer_QueryReceived(object sender, QueryReceivedEventArgs eventArgs)
    {
        if (eventArgs.Query is not DnsMessage message) return;

        DnsMessage response = message.CreateResponseInstance();
        response.AnswerRecords.Clear();

        string fakeDomain = "msasanmh.net";

        try
        {
            for (int n = 0; n < message.Questions.Count; n++)
            {
                DnsQuestion dnsQuestion = message.Questions[n];
                //Debug.WriteLine("========> Question: " + dnsQuestion.Name);
                string questionName = dnsQuestion.Name.ToString().ToLower().Trim();
                if (questionName.EndsWith('.')) questionName = questionName[..^1];
                Debug.WriteLine("========> Question Name: " + questionName);
                Debug.WriteLine("========> RecordType: " + dnsQuestion.RecordType);

                fakeDomain = questionName;

                DohHost = DohHost.ToLower().Trim();

                if (questionName.Equals(DohHost, StringComparison.OrdinalIgnoreCase))
                {
                    ARecord aRecord1 = new(DomainName.Parse(DohHost), 60, IPAddress.Parse(DohCleanIP));
                    response.AnswerRecords.Add(aRecord1);
                }
                else if (questionName.Equals($"{DohHost}:{DohPort}", StringComparison.OrdinalIgnoreCase))
                {
                    ARecord aRecord1 = new(DomainName.Parse($"{DohHost}:{DohPort}"), 60, IPAddress.Parse(DohCleanIP));
                    response.AnswerRecords.Add(aRecord1);
                }
                else
                {
                    // Get IPv4
                    string ipv4 = await GetIP.GetIpFromPlainDNS(questionName, "8.8.8.8", 53, 3);
                    if (!string.IsNullOrEmpty(ipv4))
                    {
                        ARecord aRecord1 = new(dnsQuestion.Name, 60, IPAddress.Parse(ipv4));
                        response.AnswerRecords.Add(aRecord1);
                    }
                }
            }
        }
        catch (Exception) { }

        if (!response.AnswerRecords.Any())
        {
            ARecord aRecord1 = new(DomainName.Parse(fakeDomain), 60, IPAddress.Parse("0.0.0.0"));
            response.AnswerRecords.Add(aRecord1);
        }

        // Set the response
        foreach(DnsRecordBase ar in response.AnswerRecords)
            Debug.WriteLine("========> Response: " + ar.ToString());
        eventArgs.Response = response;
    }

    //private async Task DnsServer_QueryReceived(object sender, QueryReceivedEventArgs eventArgs)
    //{
    //    if (eventArgs.Query is not DnsMessage message) return;

    //    DnsMessage response = message.CreateResponseInstance();

    //    if ((message.Questions.Count == 1))
    //    {
    //        // send query to upstream server
    //        DnsQuestion question = message.Questions[0];
    //        DnsMessage? upstreamResponse = await DnsClient.Default.ResolveAsync(question.Name, question.RecordType, question.RecordClass);

    //        // if got an answer, copy it to the message sent to the client
    //        if (upstreamResponse != null)
    //        {
    //            // Adding Records
    //            foreach (DnsRecordBase record in (upstreamResponse.AnswerRecords))
    //            {
    //                response.AnswerRecords.Add(record);
    //                Debug.WriteLine("========> Record: " + record.Name);
    //            }
    //            foreach (DnsRecordBase record in (upstreamResponse.AdditionalRecords))
    //            {
    //                response.AdditionalRecords.Add(record);
    //            }

    //            response.ReturnCode = ReturnCode.NoError;

    //            // If it's Cloudflare
    //            string host1 = "dns.cloudflare.com";
    //            string host2 = "cloudflare-dns.com";
    //            string host3 = "every1dns.com";
    //            if (DohHost.Equals(host1) || DohHost.Equals(host2) || DohHost.Equals(host3))
    //            {
    //                if (message.Questions[0].Name.Equals(DomainName.Parse("dns.cloudflare.com")))
    //                {
    //                    response.AnswerRecords.Clear();
    //                    if (message.Questions[0].RecordType == RecordType.A)
    //                    {
    //                        ARecord aRecord1 = new(DomainName.Parse("dns.cloudflare.com"), 60, IPAddress.Parse(DohCleanIP));
    //                        response.AnswerRecords.Add(aRecord1);
    //                    }
    //                }

    //                if (message.Questions[0].Name.Equals(DomainName.Parse("dns.cloudflare-dns.com")))
    //                {
    //                    response.AnswerRecords.Clear();

    //                    if (message.Questions[0].RecordType == RecordType.A)
    //                    {
    //                        ARecord aRecord1 = new(DomainName.Parse("dns.cloudflare-dns.com"), 60, IPAddress.Parse(DohCleanIP));
    //                        response.AnswerRecords.Add(aRecord1);
    //                    }

    //                    if (message.Questions[0].RecordType == RecordType.CName)
    //                    {
    //                        CNameRecord cNameRecord = new(DomainName.Parse("dns.cloudflare-dns.com"), 60, DomainName.Parse("every1dns.com"));
    //                        response.AnswerRecords.Add(cNameRecord);
    //                    }
    //                }

    //                if (message.Questions[0].Name.Equals(DomainName.Parse("every1dns.com")))
    //                {
    //                    response.AnswerRecords.Clear();

    //                    if (message.Questions[0].RecordType == RecordType.A)
    //                    {
    //                        ARecord aRecord1 = new(DomainName.Parse("every1dns.com"), 60, IPAddress.Parse(DohCleanIP));
    //                        response.AnswerRecords.Add(aRecord1);
    //                    }
    //                }
    //            }
    //            else
    //            {
    //                // If it's not Cloudflare
    //                if (message.Questions[0].Name.Equals(DomainName.Parse(DohHost)))
    //                {
    //                    response.AnswerRecords.Clear();
    //                    if (message.Questions[0].RecordType == RecordType.A)
    //                    {
    //                        ARecord aRecord1 = new(DomainName.Parse(DohHost), 60, IPAddress.Parse(DohCleanIP));
    //                        response.AnswerRecords.Add(aRecord1);
    //                    }
    //                }

    //                if (message.Questions[0].Name.Equals(DomainName.Parse($"{DohHost}:{DohPort}")))
    //                {
    //                    response.AnswerRecords.Clear();
    //                    if (message.Questions[0].RecordType == RecordType.A)
    //                    {
    //                        ARecord aRecord1 = new(DomainName.Parse($"{DohHost}:{DohPort}"), 60, IPAddress.Parse(DohCleanIP));
    //                        response.AnswerRecords.Add(aRecord1);
    //                    }
    //                }
    //            }

    //            // Set the response
    //            if (response.AnswerRecords.Any())
    //                Debug.WriteLine("========> Response: " + response.AnswerRecords[0].ToString());
    //            eventArgs.Response = response;
    //        }
    //    }
    //}
}