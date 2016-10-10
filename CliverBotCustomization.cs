//********************************************************************************************
//Author: Sergey Stoyan, CliverSoft.com
//        http://cliversoft.com
//        stoyan@cliversoft.com
//        sergey.stoyan@gmail.com
//        27 February 2007
//Copyright: (C) 2007, Sergey Stoyan
//********************************************************************************************

using System;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web;
using System.Data;
using System.Web.Script.Serialization;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Net.Mail;
using Cliver;
using System.Configuration;
using System.Windows.Forms;
//using MySql.Data.MySqlClient;
using Cliver.Bot;
using Cliver.BotGui;
using Microsoft.Win32;
using System.Reflection;

namespace Cliver.BotCustomization
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            //Cliver.CrawlerHost.Linker.ResolveAssembly();
            main();
        }

        static void main()
        {
            //Cliver.Bot.Program.Run();//It is the entry when the app runs as a console app.
            Cliver.BotGui.Program.Run();//It is the entry when the app uses the default GUI.
        }
    }

    public class CustomBotGui : Cliver.BotGui.BotGui
    {
        override public string[] GetConfigControlNames()
        {
            return new string[] { "General", "Input", "Output", "Web", /*"Browser", "Spider",*/ "Proxy", "Log" };
        }

        override public Cliver.BaseForm GetToolsForm()
        {
            return null;
        }

        //override public Type GetBotThreadControlType()
        //{
        //    return typeof(IeRoutineBotThreadControl);
        //    //return typeof(WebRoutineBotThreadControl);
        //}
    }

    public class CustomBot : Cliver.Bot.Bot
    {
        new static public string GetAbout()
        {
            return @"WEB CRAWLER
Created: " + Cliver.Bot.Program.GetCustomizationCompiledTime().ToString() + @"
Developed by: www.cliversoft.com";
        }

        new static public void SessionCreating()
        {
            //InternetDateTime.CHECK_TEST_PERIOD_VALIDITY(2016, 10, 7);

            FileWriter.This.WriteHeader(
               "Name",
               "City",
                        "ZipCode",
               "State",
               "Phone",
               "Email",
               "Url",
               "Url2"
            );
        }

        new static public void SessionClosing()
        {
        }

        override public void CycleBeginning()
        {
            //IR = new IeRoutine(((IeRoutineBotThreadControl)BotThreadControl.GetInstanceForThisThread()).Browser);
            //IR.UseCache = false;
            HR = new HttpRoutine();
        }

        //IeRoutine IR;

        HttpRoutine HR;

        public class StateItem : InputItem
        {
            //readonly public string City;
            readonly public string State;

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;
                string url = "http://www.rent.com/" + State;
                if (!cb.HR.GetPage(url))
                    throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url);

                DataSifter.Capture c = cities.Parse(cb.HR.HtmlResult);

                string[] us = c.ValuesOf("Url");
                for (int i = 0; i < us.Length; i++)
                    bc.Add(new SearchItem("http://www.rent.com" + us[i]));
            }
            static DataSifter.Parser cities = new DataSifter.Parser("cities.fltr");
        }

        public class SearchItem : InputItem
        {
            readonly public string Url;

            public SearchItem(string url)
            {
                Url = url;
            }

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;
                cb.search_processor(Url);
            }
        }

        void search_processor(string url)
        {
            if (!HR.GetPage(url))
                throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url);

            DataSifter.Capture c0 = search.Parse(HR.HtmlResult);

            string npu = c0.ValueOf("NextPageUrl");
            if (npu != null)
                BotCycle.Add(new SearchNextPageItem(npu));

            foreach (DataSifter.Capture c in c0["Product"])
                BotCycle.Add(new CompanyItem(Spider.GetAbsoluteUrl(c.ValueOf("Url"), url), c.ValueOf("Name"), c.ValueOf("City"), c.ValueOf("State"), c.ValueOf("Phone")));
        }
        static DataSifter.Parser search = new DataSifter.Parser("search.fltr");

        public class SearchNextPageItem : InputItem
        {
            readonly public string Url;

            public SearchNextPageItem(string url)
            {
                Url = url;
            }

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;
                cb.search_processor(Url);
            }
        }

        public class CompanyItem : InputItem
        {
            readonly public string Url;
            readonly public string Name;
            readonly public string City;
            readonly public string State;
            readonly public string Phone;

            public CompanyItem(string url, string name, string city, string state, string phone)
            {
                Url = url;
                Name = name;
                City = city;
                State = state;
                Phone = phone;
            }

            override public void PROCESSOR(BotCycle bc)
            {
                CustomBot cb = (CustomBot)bc.Bot;

                string name = FieldPreparation.Html.GetCsvField(Name);

                if (!cb.HR.GetPage(Url))
                    throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + Url);

                DataSifter.Capture c = product.Parse(cb.HR.HtmlResult);
                string zip_code = Regex.Replace(c.ValueOf("ZipCode"), @"[^\d]", "", RegexOptions.Singleline);
                string url2 = "http://www.yellowpages.com/search?search_terms=" + name + "&geo_location_terms=" + zip_code;
                if (!cb.HR.GetPage(url2))
                    throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url2);

                DataSifter.Capture c2 = yp.Parse(cb.HR.HtmlResult);
                string regex_name = get_stripped_name(name);
                regex_name = Regex.Escape((regex_name.Length > 10 ? regex_name.Substring(0, 10) : regex_name).Trim());
                string email = null;
                string url3 = url2;
                foreach (DataSifter.Capture cc in c2["Company"])
                    if (cc.ValueOf("ZipCode") != null
                        && Regex.Replace(cc.ValueOf("ZipCode"), @"[^\d]", "", RegexOptions.Singleline) == zip_code
                        && Regex.IsMatch(get_stripped_name(cc.ValueOf("Name")), regex_name, RegexOptions.IgnoreCase)
                        )
                    {
                        url3 = Spider.GetAbsoluteUrl(cc.ValueOf("Url"), url2);
                        if (!cb.HR.GetPage(url3))
                            throw new ProcessorException(ProcessorExceptionType.RESTORE_AS_NEW, "Could not get: " + url3);

                        DataSifter.Capture c3 = yp2.Parse(cb.HR.HtmlResult);
                        email = c3.ValueOf("Email");
                        break;
                    }

                FileWriter.This.PrepareAndWriteHtmlLineWithHeader(
                    "Name", Name,
                    "City", City,
                    "ZipCode", zip_code,
                    "State", State,
                    "Phone", Phone,
                    "Email", email,
                    "Url", Url,
                    "Url2", url3
                    );
            }
            static DataSifter.Parser product = new DataSifter.Parser("product.fltr");
            static DataSifter.Parser yp = new DataSifter.Parser("yp.fltr");
            static DataSifter.Parser yp2 = new DataSifter.Parser("yp2.fltr");

            static string get_stripped_name(string name)
            {
                return FieldPreparation.Html.GetCsvField(Regex.Replace(name, @"(the|a)\s", "", RegexOptions.IgnoreCase));
            }
        }
    }
}