# DEBATE AI · Pre-Launch Website

Next.js + TypeScript + Tailwind. German responsive landing page with interactive scripted debate demo, three draft subscriptions, FAQ and a Supabase-backed waitlist.

## Run locally

1. Install Node.js 20 or later and npm.
2. In this directory, run `npm install`.
3. Run `npm run dev` and open http://localhost:3000.
4. Optional: `npm run typecheck` and `npm run build`.

The page and demo work without a database. The waitlist correctly returns **503** until you configure Supabase, and never pretends to store an entry.

## Connect waitlist

1. Create a Supabase project.
2. Run `db/schema.sql` in Supabase SQL editor.
3. Copy `.env.example` to `.env.local` and fill `SUPABASE_URL` and `SUPABASE_SERVICE_ROLE_KEY` using the Supabase project settings.
4. Restart Next.js. Test a real form submission, then check the `waitlist` table.
5. Keep the **service-role key server-side**; never use `NEXT_PUBLIC_` for it and never commit `.env.local`.

For production, add IP-based rate limits/CAPTCHA on the API route, a double-opt-in email confirmation and unsubscribe flow, privacy/retention rules, proper legal notice and privacy policy, and consent record policies appropriate to your market. The current endpoint has basic validation and a honeypot but **not** strong bot protection.

## Important honesty notes

- The example demo is scripted, **not** a real voice assistant.
- No subscription is sold or charged. Prices are indicative.
- This website does not connect to TikTok LIVE or the separate Windows MVP yet.
- Do not publicly launch in Germany until imprint and privacy policy are finalized.
