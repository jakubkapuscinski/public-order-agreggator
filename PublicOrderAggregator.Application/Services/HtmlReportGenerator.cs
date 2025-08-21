using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;

namespace PublicOrderAggregator.Application.Services
{
    public class HtmlReportGenerator : IReportGenerator
    {
        public async Task<string> GenerateHtmlReportAsync(IEnumerable<PublicOrder> orders)
        {
            var ordersHtml = new StringBuilder();
            
            foreach (var order in orders.OrderByDescending(o => o.SubmissionDeadline))
            {
                var isUrgent = order.SubmissionDeadline <= DateTime.Now.AddDays(7) ? "urgent" : "";
                
                ordersHtml.AppendLine($@"
            <article class=""order-card"">
                <div class=""card-header"">
                    <div class=""card-title"">
                        <a href=""{order.OriginalUrl}"" target=""_blank"">
                            {order.Subject}
                        </a>
                        <span class=""api-badge"">{order.DataSource.ToUpper()}_API</span>
                    </div>
                </div>

                <div class=""card-details"">
                    <div class=""detail-item"">
                        <div class=""detail-label"">Organizator</div>
                        <div class=""detail-value"">{order.Organizer}</div>
                    </div>
                    <div class=""detail-item"">
                        <div class=""detail-label"">Lokalizacja</div>
                        <div class=""detail-value"">{order.Location}</div>
                    </div>
                    <div class=""detail-item"">
                        <div class=""detail-label"">Data przetargu</div>
                        <div class=""detail-value"">{order.TenderDate:dd.MM.yyyy}</div>
                    </div>
                    <div class=""detail-item"">
                        <div class=""detail-label"">Termin składania</div>
                        <div class=""detail-value {isUrgent}"">{order.SubmissionDeadline:yyyy-MM-dd}</div>
                    </div>
                </div>

                <div class=""ai-reasoning-section"">
                    <div class=""reasoning-header"">🤖 Decyzja AI - Dlaczego ten przetarg został wybrany</div>
                    <div class=""reasoning-content"">
                        {(!string.IsNullOrEmpty(order.ClassificationReasoning) ? order.ClassificationReasoning : "Brak uzasadnienia AI")}
                    </div>
                </div>

                <div class=""requirements-section"">
                    <div class=""requirements-header"">Wymagania</div>
                    <div class=""requirements-content"">
                        {order.Requirements}
                    </div>
                </div>
            </article>");
            }

            var html = $@"<!DOCTYPE html>
<html lang=""pl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Raport Zamówień Publicznych - {DateTime.Now:yyyy-MM-dd}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            line-height: 1.6;
            color: #1a1a1a;
            background: #f8fafc;
            font-size: 15px;
        }}

        .container {{
            max-width: 1000px;
            margin: 0 auto;
            padding: 40px 20px;
        }}

        .header {{
            text-align: center;
            margin-bottom: 50px;
            padding-bottom: 30px;
            border-bottom: 1px solid #e5e5e5;
        }}

        .header h1 {{
            font-size: 32px;
            font-weight: 600;
            color: #1a1a1a;
            margin-bottom: 8px;
            letter-spacing: -0.5px;
        }}

        .header .subtitle {{
            color: #666;
            font-size: 15px;
            font-weight: 400;
        }}

        .stats {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 40px;
        }}

        .stat-card {{
            background: #ffffff;
            border: 1px solid #d1d5db;
            border-radius: 12px;
            padding: 20px;
            text-align: center;
            box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
        }}

        .stat-number {{
            font-size: 28px;
            font-weight: 600;
            color: #1a1a1a;
            margin-bottom: 4px;
        }}

        .stat-label {{
            color: #666;
            font-size: 14px;
            font-weight: 400;
        }}

        .cards-container {{
            display: flex;
            flex-direction: column;
            gap: 24px;
        }}

        .order-card {{
            background: #ffffff;
            border: 1px solid #d1d5db;
            border-radius: 12px;
            overflow: hidden;
            transition: all 0.2s ease;
            box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
        }}

        .order-card:hover {{
            border-color: #2563eb;
            box-shadow: 0 4px 12px rgba(37, 99, 235, 0.15);
            transform: translateY(-2px);
        }}

        .card-header {{
            padding: 24px 24px 20px;
            border-bottom: 1px solid #f5f5f5;
        }}

        .card-title {{
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            gap: 16px;
        }}

        .card-title a {{
            color: #2563eb;
            text-decoration: none;
            font-size: 18px;
            font-weight: 500;
            flex: 1;
            line-height: 1.4;
        }}

        .card-title a:hover {{
            text-decoration: underline;
        }}

        .api-badge {{
            background: #f8fafc;
            color: #475569;
            padding: 4px 12px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 500;
            border: 1px solid #e2e8f0;
            white-space: nowrap;
        }}

        .card-details {{
            padding: 20px 24px;
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 20px;
        }}

        .detail-item {{
            padding: 16px 0;
        }}

        .detail-label {{
            font-size: 12px;
            font-weight: 500;
            color: #64748b;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 4px;
        }}

        .detail-value {{
            font-size: 15px;
            font-weight: 500;
            color: #1a1a1a;
        }}

        .detail-value.urgent {{
            color: #dc2626;
            font-weight: 600;
        }}

        .requirements-section {{
            padding: 20px 24px 24px;
            background: #f8fafc;
            border-top: 1px solid #e5e7eb;
        }}

        .requirements-header {{
            font-size: 12px;
            font-weight: 600;
            color: #1f2937;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 12px;
        }}

        .requirements-content {{
            font-size: 14px;
            line-height: 1.7;
            color: #374151;
        }}

        .ai-reasoning-section {{
            padding: 20px 24px;
            background: #eff6ff;
            border-top: 1px solid #e5e7eb;
            border-left: 4px solid #3b82f6;
        }}

        .reasoning-header {{
            font-size: 12px;
            font-weight: 600;
            color: #1e40af;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
            display: flex;
            align-items: center;
            gap: 6px;
        }}

        .reasoning-content {{
            font-size: 14px;
            line-height: 1.6;
            color: #1e40af;
            font-style: italic;
            background: #ffffff;
            padding: 12px 16px;
            border-radius: 8px;
            border: 1px solid #dbeafe;
        }}

        @media (max-width: 768px) {{
            .container {{
                padding: 30px 16px;
            }}

            .header h1 {{
                font-size: 28px;
            }}

            .card-title {{
                flex-direction: column;
                gap: 12px;
            }}

            .api-badge {{
                align-self: flex-start;
            }}

            .card-details {{
                grid-template-columns: 1fr;
                gap: 16px;
                padding: 16px 20px;
            }}

            .card-header {{
                padding: 20px;
            }}

            .requirements-section {{
                padding: 16px 20px 20px;
            }}

            .detail-item {{
                padding: 12px 0;
            }}

            .stats {{
                grid-template-columns: repeat(2, 1fr);
                gap: 16px;
            }}
        }}

        @media (max-width: 480px) {{
            .container {{
                padding: 20px 12px;
            }}

            .header {{
                margin-bottom: 30px;
            }}

            .header h1 {{
                font-size: 24px;
            }}

            .card-header {{
                padding: 16px;
            }}

            .card-details {{
                padding: 12px 16px;
            }}

            .requirements-section {{
                padding: 12px 16px 16px;
            }}

            .stats {{
                grid-template-columns: 1fr;
            }}
        }}
    </style>
</head>

<body>
    <div class=""container"">
        <header class=""header"">
            <h1>Raport Zamówień Publicznych</h1>
            <div class=""subtitle"">Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm}</div>
        </header>

        <div class=""stats"">
            <div class=""stat-card"">
                <div class=""stat-number"">{orders.Count()}</div>
                <div class=""stat-label"">Łączna liczba zamówień</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-number"">{orders.Count(o => o.SubmissionDeadline > DateTime.Now)}</div>
                <div class=""stat-label"">Aktywne zamówienia</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-number"">{orders.Select(o => o.Organizer).Distinct().Count()}</div>
                <div class=""stat-label"">Unikalni organizatorzy</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-number"">{orders.Count(o => o.SubmissionDeadline.Date >= DateTime.Now.Date && o.SubmissionDeadline.Date <= DateTime.Now.AddDays(7).Date)}</div>
                <div class=""stat-label"">Kończące się w tym tygodniu</div>
            </div>
        </div>

        <div class=""cards-container"">
            {ordersHtml}
        </div>
    </div>
</body>
</html>";

            return await Task.FromResult(html);
        }
    }
}