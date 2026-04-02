# Azure AI Search Demo Script: Traditional vs Agentic Retrieval

## 🎯 Objective
Show how Azure AI Search evolves from a **search engine** into an **AI-ready retrieval system**, and explicitly highlight the features that enable it.

## 🖼️ Demo UI Snapshot
![Frontend demo UI](images/screenshot-frontend.png)

---

# 🧭 1. Framing (30 sec)

**Say:**

> What you’re about to see is not just a chatbot.
> It’s a retrieval system where the answer depends on who you are—and how retrieval is done.

> Same question. Different answers. Two different retrieval models.

---

# 🧩 2. Establish Trust (Security Trimming) (1–2 min)

**Action:**
Ask:

> What are the pricing terms for our premium agreement?

Run as:

* Guest
* Authenticated user

**Say:**

> Notice how the answers differ. That’s not the model behaving differently—it’s retrieval.

> We use Azure AI Search **security trimming**, which means access control is enforced at query time before the model sees any data.

---

# ⚙️ 3. Show the Machinery (Traditional Retrieval) (1 min)

**Action:**
Show logs / retrieval panel:

* Query
* Filters
* Authorization header

**Say:**

> This is traditional retrieval using Azure AI Search.

> We explicitly construct the query using:
>
> * **Hybrid search** (keyword + vector)
> * **Semantic ranking** for relevance
> * **Filters and ACLs** for access control

> This gives us full control—but we also have to build and maintain it.

---

# 🔥 4. Introduce Tension (20 sec)

**Say:**

> This works well. But it means we are responsible for designing the entire retrieval logic.

> So what happens if we don’t?

---

# 🚀 5. Switch to Agentic Retrieval (Knowledge Base) (2 min)

**Action:**
Switch mode → `Agentic`

Ask the SAME question again.

**Show logs / panel:**

* No query
* Knowledge base call

**Say:**

> Now we’re using Azure AI Search **Knowledge Base**, which is part of agentic retrieval.

> Notice:
>
> * No query construction
> * No manual ranking logic

> The platform now handles:
>
> * Query rewriting
> * Chunking
> * Ranking

> We’ve moved from **query design → knowledge design**

---

# 💥 6. Aha Moment (Reasoning over Retrieval) (1–2 min)

**Action:**
Ask:

> Compare liability terms between Contoso and Fabrikam

**Say:**

> This is where it gets interesting.

> This isn’t a single query anymore. The system performs **multi-step retrieval** behind the scenes.

> Azure AI Search is no longer just retrieving—it’s supporting **LLM-driven reasoning over data**.

---

# 🧪 7. Failure Mode (Grounded Answers) (30 sec)

**Action:**
Ask:

> What is our policy for Martian customers?

**Say:**

> The system returns no answer.

> That’s because we enforce **grounded retrieval**—the model can only answer based on retrieved content.

> No data → no answer → no hallucination.

---

# 🧠 8. Close (30 sec)

**Say:**

> Azure AI Search gives us:
>
> * Hybrid search
> * Semantic ranking
> * Built-in security trimming

> And now:
>
> * A knowledge layer that makes retrieval usable for AI systems

> Elastic gives you search primitives.
> Azure AI Search gives you retrieval patterns for AI.

---

# 📊 Feature Mapping Table (for reinforcement)

| Demo step        | What you showed     | Azure AI Search feature                               | Why it matters                       |
| ---------------- | ------------------- | ----------------------------------------------------- | ------------------------------------ |
| Guest vs Auth    | Different answers   | Security trimming (`x-ms-query-source-authorization`) | Access control enforced at retrieval |
| Good answers     | Relevant results    | Hybrid search (keyword + vector)                      | Better recall                        |
| Ranking quality  | Precise answers     | Semantic ranker                                       | Better precision                     |
| Logs             | Visible query logic | Filters + scoring profiles                            | Full control                         |
| Agentic mode     | No query            | Knowledge Base                                        | Removes retrieval complexity         |
| Complex question | Comparison answer   | Multi-step retrieval support                          | Enables reasoning                    |
| Failure case     | “I don’t know”      | Grounded retrieval                                    | Prevents hallucination               |
| Citations        | Sources shown       | Chunking + retrieval context                          | Transparency                         |
| Freshness        | Recent info         | Scoring profiles (boosting)                           | Business relevance                   |

---

# 🎯 Key takeaway

> Azure AI Search is not just a search engine.
> It’s a retrieval system designed for AI.

> You can control everything—or let the platform handle it.

---
