# Azure AI Search Demo Script: Traditional vs Agentic Retrieval

## 🎯 Objective
Demonstrate the shift from controlled, manual retrieval to platform-driven, agentic retrieval in Azure AI Search.

---

## 🧭 1. Framing (30 sec)

**Say:**
> What you're about to see is not a chatbot. It's a controlled retrieval system where the answer depends on who you are and how retrieval is done.

> Same question. Different answers. Two different retrieval models.

---

## 🧩 2. Establish Trust (1–2 min)

**Action:**
Ask:
> What are the pricing terms for our premium agreement?

Run as:
- Guest
- Authenticated user

**Say:**
> We don't trust the model. We control what it sees.

---

## ⚙️ 3. Show the Machinery (1 min)

**Action:**
Show logs:
- request payload construction
- index selection (public-index vs entitled-index)
- headers (x-ms-query-source-authorization)

**Say:**
> This is traditional retrieval. We explicitly construct the search request and enforce access at retrieval time.

---

## 🔥 4. Introduce Tension (20 sec)

**Say:**
> This works. But it requires us to design and maintain all retrieval logic ourselves.

> So what happens if we don't do that?

---

## 🚀 5. Switch to Agentic Retrieval (2 min)

**Action:**
Toggle:
mode = agentic

Ask same question again.

**Show logs:**
- Traditional: explicit search request payload
- Agentic: single knowledge base call

**Say:**
> We removed the retrieval logic. The platform now decides how to search, rank, and chunk.

---

## 💥 6. Aha Moment (1–2 min)

**Action:**
Ask:
> Compare liability terms between Contoso and Fabrikam

**Say:**
> This is where retrieval stops being a query—and becomes reasoning.

---

## 🧪 7. Failure Mode (30 sec)

**Action:**
Ask:
> What is our policy for Martian customers?

**Say:**
> The system only answers from approved sources. No hallucination fallback.

---

## 🧠 8. Close (30 sec)

**Say:**
> Elastic gives you search primitives. Azure AI Search gives you retrieval patterns for AI.

> You can still control everything—but you don't always have to.
