﻿using System;
using System.Collections.Generic;
using Bars.EAS.Utils.Extension;
using BM;
using BM.Core;
using BM.DTO;
using Dafabet.Models;
using Newtonsoft.Json;
using Scanner.Helper;

namespace Dafabet
{
    public class DafabetConverter
    {
        private List<LineDTO> _lines;

        public LineDTO[] Convert(MatchDataResult data, string bookmakerName)
        {
            _lines = new List<LineDTO>();

            //var data = JsonConvert.DeserializeObject<MatchDataResult>(response);

            foreach (var league in data.leagues)
            {
                foreach (var match in league.matches)
                {
                    var lineTemplate = new LineDTO();

                    lineTemplate.SportKind = Helper.ConvertSport(league.SportName);
                    lineTemplate.BookmakerName = bookmakerName;

                    lineTemplate.Team1 = match.HomeName;
                    lineTemplate.Team2 = match.AwayName;

                    lineTemplate.Score1 = match.MoreInfo.ScoreH;
                    lineTemplate.Score2 = match.MoreInfo.ScoreA;

                    foreach (var oddSet in match.oddset)
                    {
                        var lineTemplate2 = lineTemplate.Clone();

                        lineTemplate2.CoeffType = GetCoeffType(oddSet.Bettype);

                        foreach (var setSel in oddSet.sels)
                        {
                            var lineTemplate3 = lineTemplate2.Clone();

                            var coeffKind = GetCoeffKind(oddSet.Bettype, setSel.Key, out var hasParam);

                            //ProxyHelper.UpdateDafabetEvents($"SportName {league.SportName} | SportType {league.SportType} | Bettype {oddSet.Bettype} | OddsId {oddSet.OddsId} | Key Point Price {setSel.Key} {setSel.Point} {setSel.Price}");

                            if (coeffKind.IsEmpty()) continue;

                            lineTemplate3.CoeffKind = coeffKind;

                            if (hasParam) lineTemplate3.CoeffParam = coeffKind == "HANDICAP1" ? -1 * setSel.Point : setSel.Point;

                            decimal price;

                            if (setSel.Price > 0 && setSel.Price <= 1)
                                price = setSel.Price + 1m;
                            else if (setSel.Price >= -1 && setSel.Price < 0)
                                price = -1m / setSel.Price + 1m;
                            else
                                price = /*-1m**/ setSel.Price;

                            lineTemplate3.CoeffValue = decimal.Round(price, 2, MidpointRounding.AwayFromZero);

                            lineTemplate3.LineObject = $"{oddSet.OddsId}|{setSel.Key}|{oddSet.Bettype}|{setSel.Price}";

                            lineTemplate3.UpdateName();

                            AddLine(lineTemplate3);
                        }
                    }
                }
            }

            return _lines.ToArray();
        }

        private string GetCoeffType(int betType)
        {
            switch (betType)
            {
                case 7:
                case 8:
                case 12:
                case 15:
                case 410:
                    return "1st half";
                case 431:
                case 428:
                    return "2nd half";
                default:
                    return null;
            }
        }

        private static string GetCoeffKind(int betType, string betteam, out bool hasParam)
        {
            string result;

            hasParam = true;

            switch (betType)
            {
                //HANDICAP
                case 1:
                //1H HANDICAP
                case 7:

                    result = "HANDICAP";

                    if (betteam == "h")
                        result += "1";
                    else if (betteam == "a")
                        result += "2";
                    else
                        return string.Empty;

                    return result;


                //OVER/UNDER
                case 3:
                //1H OVER/UNDER
                case 8:
                    result = "TOTAL";

                    if (betteam == "h")
                        result += "OVER";
                    else if (betteam == "a")
                        result += "UNDER";
                    else
                        return string.Empty;

                    return result;

                //1X2
                case 5:
                //1H 1X2
                case 15:

                //1H Double chance
                case 410:
                //2H Double chance
                case 431:
                //Double chance
                case 24:

                    switch (betteam)
                    {
                        case "hd":
                            result = "1x";
                            break;
                        case "ha":
                            result = "12";
                            break;
                        case "da":
                            result = "x2";
                            break;
                        default:
                            result = betteam;
                            break;
                    }

                    hasParam = false;

                    return result;
            }

            return string.Empty;
        }

        private void AddLine(LineDTO lineDto)
        {
            lineDto.UpdateName();
            _lines.Add(lineDto);
        }

    }
}


