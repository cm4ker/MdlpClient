namespace MdlpApiClient
{
    using System;
    using System.Linq;
    using MdlpApiClient.Xsd;
    using MdlpApiClient.Serialization;
    using System.Text;

    /// <remarks>
    /// Strongly typed REST API methods. Chapter 5: documents.
    /// </remarks>
    partial class MdlpClient
    {
        /// <summary>
        /// 5.1. Отправка объекта документа
        /// </summary>
        /// <param name="doc">Объект документа</param>
        /// <returns>Идентификатор документа</returns>
        public string SendDocument(Documents doc)
        {
            if (!LargeDocumentSize.HasValue)
            {
                LargeDocumentSize = GetLargeDocumentSize();
            }

            // serialize the document and estimate data packet size
            var xml = XmlSerializationHelper.Serialize(doc, ApplicationName);
            var xmlBytes = Encoding.UTF8.GetByteCount(xml);
            var xmlBase64 = 4 * xmlBytes / 3;
            var overhead = 1024; // requestId + JSON serialization overhead
            var totalSize = xmlBase64 + SignatureSize + overhead;

            // prefer SendDocument for small documents
            if (totalSize < LargeDocumentSize)
            {
                return SendDocument(xml);
            }

            return SendLargeDocument(xml);
        }

        /// <summary>
        /// Ограничение на размер документа, который можно отсылать методом SendDocument.
        /// </summary>
        public int? LargeDocumentSize { get; set; }

        /// <summary>
        /// Приблизительный размер подписи для оценки размера отсылаемого пакета.
        /// Размер сигнатуры вычисляется при аутентификации резидента.
        /// </summary>
        public int SignatureSize { get; set; }

        /// <summary>
        /// 5.10. Получение объекта документа по идентификатору
        /// </summary>
        /// <param name="documentId">Идентификатор документа</param>
        public Documents GetDocument(string documentId)
        {
            var xml = GetDocumentText(documentId);
            return XmlSerializationHelper.Deserialize(xml);
        }

        /// <summary>
        /// 5.12. Получение объекта квитанции по номеру исходящего документа
        /// </summary>
        /// <param name="documentId">Идентификатор документа</param>
        public Documents GetTicket(string documentId)
        {
            var xml = GetTicketText(documentId);
            return XmlSerializationHelper.Deserialize(xml);
        }

        /// <summary>
        /// Convenience helper over 5.11 + 5.12: resolves document ids by request_id and downloads their tickets.
        /// Useful for FAILED_RESULT_READY cases where diagnostics are documented as available by request_id.
        /// </summary>
        /// <param name="requestId">Идентификатор запроса</param>
        public Documents[] GetTicketsByRequestId(string requestId)
        {
            var documents = GetDocumentsByRequestID(requestId)?.Documents;
            if (documents == null || documents.Length == 0)
            {
                return Array.Empty<Documents>();
            }

            return documents
                .Where(document => !string.IsNullOrWhiteSpace(document?.DocumentID))
                .Select(document => GetTicket(document.DocumentID))
                .ToArray();
        }

        /// <summary>
        /// Convenience helper over 5.11 + 5.12: resolves document ids by request_id and downloads their ticket XML payloads.
        /// </summary>
        /// <param name="requestId">Идентификатор запроса</param>
        public string[] GetTicketTextsByRequestId(string requestId)
        {
            var documents = GetDocumentsByRequestID(requestId)?.Documents;
            if (documents == null || documents.Length == 0)
            {
                return Array.Empty<string>();
            }

            return documents
                .Where(document => !string.IsNullOrWhiteSpace(document?.DocumentID))
                .Select(document => GetTicketText(document.DocumentID))
                .ToArray();
        }

        /// <summary>
        /// Convenience helper over 5.11 + 5.12: returns the first available ticket resolved by request_id.
        /// </summary>
        /// <param name="requestId">Идентификатор запроса</param>
        public Documents GetFirstTicketByRequestId(string requestId)
        {
            return GetTicketsByRequestId(requestId).FirstOrDefault();
        }
    }
}
