*[Read in English](README.en.md)*

## THÔNG TIN ĐỒ ÁN 

- **Môn học:** CS315.F21.CN2.TTNT — Máy học nâng cao (HK2 - 2025/2026)
- **Giảng viên hướng dẫn:** ThS. Đặng Việt Dũng
- **Sinh viên thực hiện:** Nguyễn Minh Nhật — MSSV 25410104
- **Trường:** Đại học Công nghệ Thông tin — ĐHQG TP.HCM
---

# RagHub — Trợ Lý Tra Cứu Tri Thức Nội Bộ (RAG POC)

RagHub là hệ thống hỏi đáp dựa trên kỹ thuật **Retrieval-Augmented Generation (RAG)**, cho phép người dùng đặt câu hỏi bằng ngôn ngữ tự nhiên trên kho tài liệu nội bộ của doanh nghiệp và nhận về câu trả lời kèm trích dẫn nguồn cụ thể. Toàn bộ hệ thống chạy trên hạ tầng nội bộ (self-hosted qua Ollama), không gửi dữ liệu tài liệu ra dịch vụ bên ngoài.

Đây là sản phẩm đồ án môn học, xây dựng ở quy mô proof-of-concept (POC): 20–50 tài liệu, khoảng 10 người dùng thử nghiệm, nhằm kiểm chứng tính khả thi của giải pháp trước khi cân nhắc triển khai quy mô lớn hơn.

---

## Bài toán

Trong doanh nghiệp thường có nhiều loại tài liệu nội bộ: quy định nhân sự, hướng dẫn kỹ thuật, đặc tả API... Khi cần tra cứu một thông tin cụ thể, nhân viên phải tự mở từng file, tìm kiếm thủ công (Ctrl+F) và không chắc đã tìm đúng chỗ hay tài liệu mới nhất.

RagHub giải quyết vấn đề này bằng cách cho phép đặt câu hỏi trực tiếp và trả về câu trả lời kèm nguồn trích dẫn, ví dụ:

> **Câu hỏi:** Nhân viên được nghỉ phép bao nhiêu ngày một năm?
> **Trả lời:** Nhân viên được nghỉ phép 12 ngày mỗi năm.
> **Nguồn:** LeavePolicy.pdf — Mục 1.1 Nghỉ phép năm (trang 2, đoạn #2)

Câu trả lời được sinh dựa trên các đoạn văn bản thực tế truy hồi từ tài liệu đã upload, không phải do mô hình tự suy diễn — mỗi câu trả lời đều đi kèm vị trí cụ thể trong tài liệu gốc để người dùng có thể đối chiếu.

---

## Các chức năng chính

- **Upload và quản lý tài liệu**: hỗ trợ PDF, DOCX, TXT, Markdown. Việc xử lý (tách đoạn, tạo embedding) chạy nền, không làm chậm giao diện khi upload.
- **Hỏi đáp có ngữ cảnh hội thoại**: hệ thống ghi nhớ các câu hỏi trước đó trong cùng phiên, nên có thể hỏi tiếp dạng "còn mục 2 thì sao?" mà vẫn hiểu đúng ý.
- **Trích dẫn nguồn ở cấp đoạn văn bản**: mỗi câu trả lời gắn kèm tên file, tiêu đề mục, số trang và vị trí ký tự cụ thể — không chỉ dừng ở tên file.
- **Tìm kiếm hybrid**: kết hợp tìm kiếm theo ngữ nghĩa (vector embedding) và tìm kiếm theo từ khóa (full-text), hòa trộn kết quả bằng thuật toán Reciprocal Rank Fusion (RRF) để tăng độ phủ.
- **Rerank bằng cross-encoder**: sau bước tìm kiếm, một mô hình rerank chuyên biệt sẽ chấm điểm lại và sắp xếp các đoạn theo độ liên quan thực tế với câu hỏi.
- **Điều chỉnh tham số truy hồi trực tiếp trên giao diện**: các thông số như số lượng đoạn lấy về, có bật rerank hay không... có thể chỉnh qua màn hình Settings, không cần khởi động lại server.
- **Bộ công cụ đánh giá chất lượng**: đo các chỉ số Recall@k, MRR và Citation Accuracy trên một tập câu hỏi mẫu đã gán nhãn sẵn (golden set).
- **Vận hành offline**: các mô hình embedding và sinh câu trả lời chạy cục bộ qua Ollama, dữ liệu không rời khỏi máy chủ.

---

## Kiến trúc tổng quan

![Kiến trúc tổng quan hệ thống RagHub](reports/FlowChart-TongQuan.png)

### Luồng xử lý tài liệu (thực hiện một lần khi upload)

![Luồng xử lý và lập chỉ mục tài liệu](reports/FlowChart-TaiLieu.png)

### Luồng hỏi đáp (thực hiện mỗi lần người dùng đặt câu hỏi)

![Luồng hỏi đáp RAG](reports/FlowChart-HoiDap.png)

---

## Công nghệ sử dụng

| Thành phần | Lựa chọn | Lý do chọn |
|------|----------|-------|
| Backend | .NET 9, ASP.NET Core 9 | Hỗ trợ async toàn phần, kiểu dữ liệu tường minh, dependency injection có sẵn |
| Frontend | React + Vite + Ant Design | Dev server khởi động nhanh, thư viện component UI dựng sẵn phù hợp làm nhanh POC |
| Database | PostgreSQL 17 + pgvector | Một datastore duy nhất lưu cả metadata lẫn vector, không cần triển khai thêm vector DB riêng |
| ORM | EF Core + Npgsql + Pgvector.EF | Toán tử `<=>` (cosine distance) dùng trực tiếp được trong LINQ/SQL |
| Embedding | BGE-M3 qua Ollama (1024 chiều) | Chạy được trên CPU, hoàn toàn offline, không phát sinh chi phí |
| Sinh câu trả lời | Gemma 4 / Qwen qua Ollama | Chạy cục bộ, dữ liệu không gửi ra ngoài |
| Reranker | bge-reranker-v2-m3 (Python sidecar) | Cải thiện đáng kể độ chính xác xếp hạng, vẫn chạy tốt trên CPU |

**Có thể thay thế bằng dịch vụ cloud qua cấu hình** (không cần sửa code):
- Embedding: OpenAI `text-embedding-3-small` (1536 chiều) — cần API key, dữ liệu được gửi ra ngoài
- Sinh câu trả lời: OpenAI `gpt-4o-mini` — cần API key, dữ liệu được gửi ra ngoài

---

## Cấu trúc thư mục

```
RagHub/
├── src/
│   ├── RagHub.API/             Controllers, DI wiring, startup
│   ├── RagHub.Core/            Domain models, interfaces, DTOs (không phụ thuộc hạ tầng)
│   ├── RagHub.Infrastructure/  EF DbContext, provider Ollama/OpenAI, repositories
│   ├── RagHub.Worker/          Xử lý indexing nền (IHostedService)
│   └── RagHub.AppHost/         .NET Aspire orchestration (tuỳ chọn)
├── portal/                     Frontend React + Vite
├── reranker/                   Sidecar Python FastAPI (cross-encoder)
├── db/
│   └── README.md               Hướng dẫn tạo schema / chạy migration
├── reports/                    Báo cáo đồ án
```

**Nguyên tắc phân tầng:** `Core` không phụ thuộc EF hay HTTP, chỉ định nghĩa interface. `Infrastructure` cài đặt các interface đó. `API` kết nối chúng qua constructor injection. Nhờ vậy việc đổi provider (ví dụ từ Ollama sang OpenAI) chỉ cần thay đổi cấu hình, không cần sửa mã nguồn.

---

## Yêu cầu môi trường

| Công cụ | Phiên bản | Vai trò |
|---------|-----------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.x | Chạy backend |
| [Node.js](https://nodejs.org/) | 20+ | Chạy frontend |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | bất kỳ | Chạy PostgreSQL qua container |
| [Ollama](https://ollama.com/) | mới nhất | Chạy các mô hình AI cục bộ |
| [Python](https://www.python.org/downloads/) | 3.11+ | Chạy reranker sidecar |

---

## Hướng dẫn cài đặt & chạy

### Bước 1 — Tải mô hình AI về máy

```bash
# Mô hình embedding — chuyển văn bản thành vector
ollama pull bge-m3

# Mô hình sinh câu trả lời
ollama pull gemma4:e4b
```

Quá trình tải có dung lượng vài GB, có thể mất vài phút tuỳ đường truyền. Ollama tự chạy nền sau khi cài đặt.

### Bước 2 — Khởi động database

```bash
docker run -d --name raghub-postgres \
  -e POSTGRES_PASSWORD=raghub_dev \
  -e POSTGRES_DB=raghub \
  -p 5433:5432 \
  pgvector/pgvector:pg17
```

PostgreSQL 17 + pgvector chạy ở cổng **5433** để tránh xung đột với một instance Postgres khác đã cài sẵn trên máy (nếu có).

### Bước 3 — Cấu hình backend

Tạo file `src/RagHub.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=raghub;Username=postgres;Password=raghub_dev"
  },
  "RagSettings": {
    "Embedding": {
      "Provider": "ollama",
      "Model": "bge-m3",
      "Dimensions": 1024
    },
    "Generation": {
      "Provider": "ollama",
      "Model": "gemma4:e4b",
      "NumCtx": 16384
    },
    "Retrieval": {
      "CandidateK": 20,
      "FinalN": 5,
      "UseHybrid": true,
      "UseReranker": true,
      "RerankerUrl": "http://localhost:8000"
    }
  }
}
```

> **Lưu ý về `NumCtx`:** Ollama mặc định giới hạn context ở 4K token. Nếu không đặt `NumCtx: 16384`, các đoạn tài liệu truyền vào sẽ bị cắt bớt một cách âm thầm, làm giảm chất lượng câu trả lời mà không có cảnh báo lỗi nào hiện ra.

### Bước 4 — Tạo schema database

```bash
dotnet ef database update \
  --project src/RagHub.Infrastructure \
  --startup-project src/RagHub.API
```

Lệnh này tạo 4 bảng: `documents`, `chunks`, `feedback`, `evaluation`. Cột `chunks.embedding` có kiểu `vector(1024)` — số chiều này cố định tại thời điểm tạo bảng và phải khớp với mô hình embedding đang sử dụng.

Kiểm tra kết quả:

```bash
docker exec raghub-postgres psql -U postgres -d raghub -c "\dt"
```

### Bước 5 — Cài đặt reranker sidecar

```bash
cd reranker
pip install -r requirements.txt
```

Lần chạy đầu tiên (ở bước sau) sẽ tự động tải mô hình `bge-reranker-v2-m3`.

> Nếu muốn tạm thời bỏ qua bước rerank, đặt `"UseReranker": false` trong cấu hình — hệ thống vẫn chạy bình thường, chỉ giảm độ chính xác xếp hạng.

### Bước 6 — Khởi động toàn bộ hệ thống

Có 2 cách, chọn một trong hai:

**Cách A — dùng .NET Aspire AppHost (khuyến nghị, chạy 1 lệnh duy nhất)**

```bash
dotnet run --project src/RagHub.AppHost
```

`RagHub.AppHost` tự khởi động và giám sát cả 3 tiến trình (reranker sidecar, backend API, frontend), theo đúng thứ tự phụ thuộc: reranker → API → frontend. PostgreSQL và Ollama vẫn coi là dịch vụ ngoài, chạy sẵn từ bước 1–2. Aspire mở kèm dashboard theo dõi log/trạng thái tại [http://localhost:15079](http://localhost:15079) (link đăng nhập kèm token in ra ở console khi khởi động).

**Cách B — chạy tay từng tiến trình riêng lẻ** (dùng khi cần debug từng service độc lập)

```bash
# Terminal 1 — reranker
cd reranker
uvicorn main:app --port 8000

# Terminal 2 — backend
dotnet run --project src/RagHub.API

# Terminal 3 — frontend
cd portal
npm install
npm run dev
```

Dù chạy theo cách nào, kết quả cuối cùng là 3 địa chỉ sau đều hoạt động:

```bash
curl http://localhost:8000/health
# {"status":"ok","model":"BAAI/bge-reranker-v2-m3"}

curl http://localhost:5079/api/health
# {"status":"healthy","database":"connected"}
```

Truy cập frontend tại [http://localhost:5173](http://localhost:5173).

---

## Hướng dẫn sử dụng

### Upload tài liệu

1. Vào mục **Upload** trên thanh điều hướng bên trái.
2. Chọn file PDF, DOCX, TXT hoặc Markdown.
3. Chọn loại tài liệu, hoặc để hệ thống tự nhận diện (Auto Detect).
4. Bấm Upload.

Tài liệu sẽ hiển thị trong màn hình **Documents** với trạng thái chuyển lần lượt `Pending → Processing → Indexed`. Nếu xử lý thất bại, trạng thái chuyển thành `Failed` kèm thông báo lỗi cụ thể; người dùng có thể bấm **Reindex** để xử lý lại.

### Xem trước các đoạn đã tách (Chunk Preview)

Bấm vào một tài liệu bất kỳ để mở **Chunk Preview**, hiển thị toàn bộ các đoạn văn bản đã được tách, kèm đường dẫn tiêu đề, số trang và vị trí ký tự tương ứng. Đây là màn hình hữu ích để kiểm tra khi câu trả lời của hệ thống không chính xác — giúp xác định xem lỗi nằm ở bước tách đoạn hay bước truy hồi.

### Hỏi đáp

Vào mục **Chat** và nhập câu hỏi. Hệ thống hỗ trợ câu hỏi tiếp nối có tham chiếu ngữ cảnh trước đó (ví dụ "còn mục 2 thì sao?"). Quy trình xử lý gồm: viết lại câu hỏi theo ngữ cảnh hội thoại, tìm kiếm hybrid, rerank kết quả, rồi sinh câu trả lời dạng stream kèm danh sách nguồn trích dẫn cụ thể.

### Đánh giá chất lượng

Vào mục **Evaluation** để chạy bộ câu hỏi mẫu (golden Q&A set) và đo các chỉ số:
- **Recall@k** — đoạn văn bản chứa đáp án đúng có xuất hiện trong top-k kết quả truy hồi hay không.
- **MRR (Mean Reciprocal Rank)** — đoạn đúng được xếp ở vị trí thứ mấy trong kết quả.
- **Citation Accuracy** — câu trả lời cuối cùng có trích dẫn đúng đoạn chứa thông tin hay không.

---

## Danh sách API chính

| Endpoint | Method | Chức năng |
|----------|--------|-----------|
| `/api/health` | GET | Kiểm tra kết nối database |
| `/api/documents` | GET | Lấy danh sách tài liệu |
| `/api/documents` | POST | Upload tài liệu (multipart/form-data) |
| `/api/documents/{id}/chunks` | GET | Lấy danh sách các đoạn (chunk) của tài liệu |
| `/api/documents/{id}/reindex` | POST | Lập chỉ mục lại một tài liệu |
| `/api/search` | POST | Truy hồi hybrid + rerank, dùng để debug |
| `/api/chat/query` | POST | Truy vấn RAG đầy đủ — body: `{ query, history? }`, trả về dạng stream SSE |
| `/api/feedback` | POST | Gửi phản hồi hữu ích / không hữu ích cho một câu trả lời |
| `/api/settings/retrieval` | GET/PUT | Đọc/ghi tham số truy hồi, áp dụng ngay không cần restart |
| `/api/evaluation` | GET/POST | Quản lý bộ câu hỏi đánh giá |
| `/api/evaluation/summary` | GET | Tổng hợp kết quả Recall@k, MRR, Citation Accuracy |

---

## Cấu hình chi tiết

Toàn bộ cấu hình liên quan đến AI nằm trong khối `RagSettings` của `appsettings.json`:

```json
{
  "RagSettings": {
    "Embedding": {
      "Provider": "ollama",        // "ollama" hoặc "openai"
      "Model": "bge-m3",
      "Dimensions": 1024           // Cố định khi tạo database, không tự ý đổi
    },
    "Generation": {
      "Provider": "ollama",        // "ollama" hoặc "openai"
      "Model": "gemma4:e4b",
      "NumCtx": 16384              // Bắt buộc để ghi đè giới hạn 4K mặc định của Ollama
    },
    "Retrieval": {
      "CandidateK": 20,            // Số chunk lấy về trước khi rerank
      "FinalN": 5,                 // Số chunk cuối cùng truyền vào LLM
      "UseHybrid": true,           // false = chỉ dùng dense search (chất lượng thấp hơn)
      "UseReranker": true,         // false = bỏ qua sidecar rerank, không gây lỗi hệ thống
      "RerankerUrl": "http://localhost:8000"
    }
  }
}
```

**Chuyển sang dùng OpenAI thay vì Ollama:**
1. Thêm API key: `dotnet user-secrets set "OpenAI:ApiKey" "sk-..."`
2. Đổi `Provider` thành `"openai"`, đổi `Dimensions` thành `1536`.
3. Xoá database cũ và chạy lại migration (do số chiều vector đã thay đổi).
4. Upload lại toàn bộ tài liệu, vì embedding cũ (BGE-M3) không tương thích với embedding mới (OpenAI).

> **Lưu ý bảo mật:** Khi dùng OpenAI, nội dung tài liệu nội bộ sẽ được gửi đến server của OpenAI để tạo embedding và sinh câu trả lời. Với tài liệu có tính nhạy cảm, nên giữ nguyên cấu hình Ollama chạy cục bộ.

---

## Chiến lược chunking (tách đoạn tài liệu)

Hệ thống tự động nhận diện loại tài liệu dựa trên định dạng tiêu đề và áp dụng chiến lược tách phù hợp:

| Loại tài liệu | Chiến lược | Cách tách |
|------|------------|-----------|
| Quy định (Policy) | Theo tiêu đề (heading-based) | Tách tại các tiêu đề đánh số (`1.1`, `2.3`, ...) |
| Tài liệu kỹ thuật (Technical) | Theo phần (section-based) | Tách tại các mục như `Overview`, `Architecture`, `Deployment`, ... |
| Đặc tả API | Theo endpoint | Tách tại từng HTTP verb (`POST /orders`, `GET /users`, ...) |

`MaxChunkSize` là giới hạn kích thước tối đa cho một đoạn — nếu một section vượt quá giới hạn này, hệ thống sẽ tự động tách đệ quy thành các đoạn nhỏ hơn. Tham số `Overlap` (độ chồng lấp giữa các đoạn) chỉ áp dụng cho các lần tách do vượt kích thước, không áp dụng cho tách theo cấu trúc.

---

## Xử lý sự cố thường gặp

**Tài liệu bị kẹt ở trạng thái `Processing`**
Worker chạy in-process trong tiến trình API — kiểm tra log console của API để xem lỗi. Nguyên nhân thường gặp là Ollama chưa chạy hoặc tên model cấu hình sai.

**Lỗi `vector(1024) dimension mismatch`**
Provider embedding trong cấu hình không khớp với số chiều của cột trong database. Nếu chuyển từ BGE-M3 (1024 chiều) sang OpenAI (1536 chiều), cần xoá bảng `chunks` và lập chỉ mục lại toàn bộ tài liệu.

**Câu trả lời không chính xác, hoặc hệ thống báo "không tìm thấy" dù tài liệu đã có thông tin**
1. Kiểm tra màn hình Chunk Preview — nội dung liên quan có được tách vào đúng đoạn hay không.
2. Gọi trực tiếp `POST /api/search` với câu hỏi đó để xem những đoạn nào thực sự được truy hồi.
3. Xác nhận `NumCtx` đã được đặt đúng (mặc định 16384) — nếu thiếu, Ollama sẽ cắt bớt context mà không báo lỗi.
4. Thử bật/tắt `UseHybrid` để xác định việc tìm kiếm dense hay sparse đang bỏ lỡ đoạn cần tìm.

**Reranker sidecar bị crash khi khởi động**
Thường do lỗi mạng khi tải model. Xem log tại `reranker/sidecar_err.log`, sau đó chạy lại `uvicorn` — quá trình tải sẽ tiếp tục. Có thể tạm đặt `UseReranker: false` để chạy hệ thống mà chưa cần reranker.

**Ollama không phản hồi**
Chạy `ollama list` để kiểm tra model đã được tải chưa. Nếu daemon chưa chạy, khởi động bằng `ollama serve`. Mặc định, API kết nối tới Ollama tại `http://localhost:11434`.

---

## Mục tiêu chất lượng (POC)

| Chỉ số | Mục tiêu |
|--------|----------|
| Retrieval Recall@k | > 80% |
| Citation Accuracy | > 80% |
| Thời gian phản hồi p95 (end-to-end) | < 10 giây |

Các chỉ số này được đo bằng bộ đánh giá tích hợp sẵn (màn hình Evaluation), chạy trên tập khoảng 50 câu hỏi mẫu đã gán nhãn.

---

