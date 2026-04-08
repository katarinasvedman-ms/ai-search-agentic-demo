# Azure AI Search — Demo Script v2
Show how Azure AI Search supports a telecom-style experience where public product discovery is open to everyone, but detailed manuals, specifications, and deployment guidance are only available to authenticated users.

---

## Presenter Mindset

This room has four different people with four different questions:

| Person | Their real question |
|---|---|
| CTO | "Is this the right foundation for the next 3 years, not just today?" |
| Security / Compliance | "How do I know agents won't access what they shouldn't?" |
| Product Owner | "Will this make our product experience meaningfully better?" |
| Architect | "How much will we still own and maintain?" |

Your job is to answer all four — in that order — without it feeling like a feature checklist.

> **The frame to hold throughout:**
> *Elastic Cloud is a great search engine you build RAG on top of.
> Azure AI Search is a retrieval layer designed to be called by agents.
> The decision is which foundation fits the applications you are planning to build.*

---

## Opening (60 sec)

**Do not start with the product. Start with their roadmap.**

**Say:**

> You have told us you are planning for future agentic development. This, done well, will eventually be driven by agents — not by humans typing queries.
>
> The decision you are making today is not really about search. It is about which retrieval foundation those agents will call in 18 months.
>
> Elastic Cloud will give you continuity and vector search. What we want to show you today is what a retrieval layer purpose-built for agents gives you on top of that — and let you decide if it matters for what you are building.

---

## Step 1 — The Semantic Gap (90 sec)
### *For: Product Owner, CTO*
### *Elastic contrast: stated, not shown*

**Action:** Traditional mode, Azure User.

Ask:

> What do I need to check after a storm?

Let the answer render. Point to the Preventive Maintenance Checklist surfacing in the results.

If a different maintenance or runbook source surfaces first, point to that source as long as it clearly references severe weather checks.

**Say:**

> The user never said Aurora. Never said RAN. Never said 6651.
>
> They said "storm." The system understood they meant hardware inspection after severe weather — and surfaced the right document.
>
> On Elastic Cloud, this query returns noise or nothing. BM25 matches on terms. "Storm" does not appear in the maintenance checklist title. Without a custom synonym file, a curated analyzer, and ongoing tuning — this fails.
>
> In this demo, traditional retrieval is using semantic ranking on top of the indexed content. That gives the system a better chance of connecting the intent of the question to the right document instead of just matching literal keywords.
>
> For your customer-facing support portal — this is the difference between a customer finding the answer and raising a support ticket.

**Debug panel:**
- Retrieval style: **Semantic ranking**
- Show result count and the citation linking to the maintenance checklist

---

## Step 2 — Security Trimming: For the Compliance Officer and the Agent
### *For: Security / Compliance, Architect*
### *This is the most important step for a grounded decision*

**Action:** Log out. Switch to **Guest**. Ask:

> What can you tell me about Aurora RAN 6651?

Note the answer — shallow, public-only, cites public documents.

Log in as **Azure User**. Ask the same question.

**Say:**

> Same question. Watch the debug panel, not the answer.
>
> Guest: public-safe product and solution content only. Authorization: guest.
>
> Azure User: protected manuals and planning content can appear. Security trimming applied by the retrieval plane.
>
> Here is why this matters for your architecture.

**Pause.**

> The important thing to focus on here is document-level ACL enforcement, also called security trimming.
>
> In either architecture, your application or orchestration layer still decides when to make a retrieval call. That is not the difference.
>
> The difference is where access is enforced. In this implementation, Azure AI Search evaluates the user's identity at query time and trims results based on document-level ACLs before anything is sent to the model.
>
> That means the model only receives chunks the user was allowed to retrieve. It cannot reason over protected content that never made it through the retrieval layer.
>
> That is the security point for your internal knowledge assistant. When an agent retrieves on behalf of a user, the retrieval layer enforces the user's access boundary before generation starts.

**Debug panel:**
- Results: **lower for guest, higher for Azure user**
- Filters: **none** vs **security trimming applied by retrieval plane**
- Authorization: **guest** vs **entitled user**

> **Why this lands:** Security/compliance gets their architecture answer. The architect sees the maintenance burden disappear. The CTO sees a trust model that scales to agentic workloads without re-engineering.

---

## Step 3 — Traditional Retrieval: You Keep Full Control (45 sec)
### *For: Architect*
### *Acknowledge what Elastic does well — then show what's beyond it*

**Action:** Stay as Azure User, Traditional mode. Keep debug panel visible.

**Say:**

> Before we go further — this mode exists.
>
> Traditional retrieval. Manual query construction with semantic ranking. You choose the index, you write the query, you own the behavior. Fully deterministic, fully auditable, fully debuggable.
>
> If you have retrieval logic that is working today on Elastic and you want to preserve it — you can. This is not a forced migration to a black box.
>
> But this is the ceiling of what Elastic Cloud gives you. What comes next is where the platform starts doing work you currently do yourself.

---

## Step 4 — Agentic Retrieval: The Platform Takes Orchestration Off Your Plate (90 sec)
### *For: Architect, CTO*

**Action:** Toggle to **Agentic mode**. Ask the same Aurora RAN 6651 question.

**Say:**

> Watch the debug panel.
>
> Retrieval type: System retrieval. Query construction: handled by system. Knowledge base: used.
>
> We still gave the system a user question. What we did not hand-author was the retrieval plan underneath it. We did not choose an index, write a search query, or define the ranking flow ourselves. We pointed the system at a knowledge base, and the platform handled the retrieval orchestration.
>
> On Elastic, you would typically own more of that retrieval planning yourself: query construction, ranking strategy, and how user context is carried through the retrieval path. That is engineering work that lives in your codebase permanently.
>
> Here, that is a platform concern. Your engineers are solving your product problems — not maintaining retrieval infrastructure.

**Debug panel:**
- Retrieval type: **System retrieval**
- Query construction: **handled by system**
- Knowledge base: **used**
- Authorization: **entitled user** — call out as session context, not as the security-trimming proof

---

## Step 5 — The Climax: An Agent Query (2 min)
### *For: Everyone — this is the moment the room shifts*

**Action:** Agentic mode, Azure User. Ask:

> Compare Aurora RAN 6651 and Nimbus Indoor 2400 for a factory deployment.

Let the answer render fully. Do not speak until it is complete.

**Say:**

> We did give the system a question. What matters here is that we did not hand-author the retrieval plan underneath it.
>
> This is the kind of question an agent or assistant would receive from a field engineer. The platform can decompose the question, retrieve across multiple documents, and synthesize a recommendation.
>
> Look at the answer. It correctly identifies that Aurora handles outdoor macro coverage and Nimbus handles indoor mobility. That conclusion is not in any single document — it was reasoned across retrieved content.
>
> On Elastic, you would typically get back ranked chunks and own more of the reasoning flow yourself. You pass those chunks to an LLM and depend on your retrieval, ranking, and prompt design to get the final answer right.
>
> Here, the knowledge base assembled the retrieval artifact before the model reasoned over it. The retrieval and the reasoning are coordinated by the platform.

**Point to debug panel:**

> If the panel shows a single knowledge base result, call out that it is not one document chunk. It is one assembled retrieval artifact created by the platform.
>
> If no citation chip is shown in the answer area, say: "In Agentic mode we only show citations when the platform returns a strong document reference. For this step, the point is the retrieval orchestration, not click-through source validation."
>
> This is what your internal knowledge assistant looks like when it is working correctly. A field engineer, a support agent, an HR assistant — they ask a complex question, they get a grounded answer, and the retrieval layer handled everything underneath.

**Debug panel:**
- Knowledge base: **used**
- Results: **call out what the panel actually shows**
- Authorization: **entitled user**
- Citation: **call out the knowledge base citation if present**

---

## Step 6 — Grounded Failure: The Trust Signal (30 sec)
### *For: Security / Compliance, CTO*

**Action:** Agentic mode. Ask:

> Which product manual covers lunar mining networks?

**Say:**

> I don't know based on the available information. If no relevant sources are retrieved, citations may be empty.
>
> That is the correct answer.
>
> An LLM sitting on top of Elastic Cloud without explicit grounding enforcement will synthesize something. It will find the least-wrong chunks and construct a plausible-sounding answer. That is how hallucinations reach your customers and your employees.
>
> Grounding is enforced here at the retrieval layer. No content retrieved means no answer generated. This is not a model behavior — it is an architecture guarantee.
>
> For a customer-facing support portal, this is the difference between a wrong answer that damages trust and a clean "I don't know" that sends the customer to a human.

---

## Close (45 sec)

**Do not summarize features. Close on the decision.**

**Say:**

> You came in today with the question if Azure AI Search is good for what you are building next.
>
> Here is what we showed you.
>
> Semantic ranking in the traditional retrieval path — so your product experience finds answers customers cannot keyword their way to.
>
> Access control enforced at the retrieval layer in the traditional path — so protected content is filtered before the model ever sees it.
>
> A traditional retrieval path when you want full control, and an agentic knowledge layer when you want the platform to handle orchestration.
>
> And grounded answer behavior that prevents hallucination by design — not by prompt engineering.
>
> The question is not whether Elastic Cloud can do search. It can.
>
> The question is whether you want to spend the next three years building retrieval infrastructure on top of it — or building products on top of a retrieval layer that was designed for agents from the start.

---

## Presenter Notes: Handling Pushback

| Pushback | Response |
|---|---|
| "Elastic has vector search now" | "Yes — and vector search is only one part of the retrieval problem. You still need to think about semantic ranking, query orchestration, grounded answer behavior, and how access control is enforced before generation." |
| "We're worried about vendor lock-in" | "You already have an Azure relationship. The question is whether you use it for infrastructure only, or whether you let it carry retrieval complexity too. The retrieval interface is an API — your applications are not rewritten." |
| "Our team knows Elastic" | "Your team keeps that knowledge — traditional mode here is familiar territory. What changes is the ceiling above it." |
| "How does pricing compare?" | "That is a conversation worth having with exact workload numbers. What we wanted to establish first is whether the capability gap justifies the conversation — and whether it does depends on your agentic roadmap." |

---

## Feature → Audience Map

| Demo moment | CTO | Security | Product Owner | Architect |
|---|---|---|---|---|
| Semantic gap / storm query | ✓ foundation argument | | ✓ customer experience | |
| Security trimming | ✓ trust model scales | ✓ strongest moment | | ✓ maintenance burden |
| Traditional mode | | | | ✓ continuity reassurance |
| Agentic mode | ✓ platform vision | | | ✓ orchestration off plate |
| Factory comparison | ✓ this is the future | | ✓ this is the product | ✓ this is the architecture |
| Grounded failure | ✓ risk reduction | ✓ architecture guarantee | ✓ customer trust | |

---

## One-line version (for the elevator after)

> *"Elastic gives you search primitives you build on. Azure AI Search gives you a retrieval layer you can control directly or let the platform orchestrate — with security trimming in the traditional path and grounded answer behavior throughout. Given what you are building, that difference is worth examining."*
