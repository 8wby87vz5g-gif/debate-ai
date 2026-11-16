import { NextRequest, NextResponse } from 'next/server';

export const runtime = 'nodejs';

const allowedRoles = new Set(['Creator', 'Livestream-Moderator', 'Beta-Tester', 'Anderes']);
function reply(message: string, status: number) {
  return NextResponse.json({ message }, { status, headers: { 'Cache-Control': 'no-store' } });
}

export async function POST(request: NextRequest) {
  const contentType = request.headers.get('content-type') || '';
  if (!contentType.includes('application/json')) return reply('JSON erforderlich.', 415);
  const length = Number(request.headers.get('content-length') || 0);
  if (length > 4096) return reply('Anfrage zu groß.', 413);
  let body: Record<string, unknown>;
  try {
    const raw = await request.text();
    if (raw.length > 4096) return reply('Anfrage zu groß.', 413);
    body = JSON.parse(raw);
    if (!body || Array.isArray(body) || typeof body !== 'object') throw new Error('Invalid');
  } catch { return reply('Ungültige Eingabe.', 400); }

  // Hidden field catches basic automated submissions, without disclosing the detection.
  if (body.website) return reply('Danke für deine Anfrage.', 200);
  const name = typeof body.name === 'string' ? body.name.trim() : '';
  const email = typeof body.email === 'string' ? body.email.trim().toLowerCase() : '';
  const handle = typeof body.handle === 'string' ? body.handle.trim() : '';
  const role = typeof body.role === 'string' ? body.role : '';
  if (!name || name.length > 80 || !email || email.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || handle.length > 80 || !allowedRoles.has(role) || body.consent !== true) {
    return reply('Bitte überprüfe deine Angaben und die Einwilligung.', 400);
  }
  const baseUrl = process.env.SUPABASE_URL;
  const serviceKey = process.env.SUPABASE_SERVICE_ROLE_KEY;
  if (!baseUrl || !serviceKey) return reply('Die Warteliste ist noch nicht freigeschaltet.', 503);
  let apiUrl: URL;
  try {
    apiUrl = new URL(baseUrl);
    if (apiUrl.protocol !== 'https:' || !apiUrl.hostname.endsWith('.supabase.co')) throw new Error('Supabase URL');
  } catch { return reply('Die Warteliste ist derzeit nicht verfügbar.', 503); }

  try {
    const response = await fetch(`${apiUrl.origin}/rest/v1/waitlist`, {
      method: 'POST',
      headers: {
        apikey: serviceKey,
        Authorization: `Bearer ${serviceKey}`,
        'Content-Type': 'application/json',
        Prefer: 'return=minimal',
      },
      body: JSON.stringify({ name, email, handle: handle || null, role, consent_at: new Date().toISOString() }),
      cache: 'no-store',
      signal: AbortSignal.timeout(8000),
    });
    if (response.status === 409) return reply('Diese E-Mail ist bereits vorgemerkt.', 200);
    if (!response.ok) {
      console.error('Waitlist database error', response.status);
      return reply('Eintragung momentan nicht möglich. Bitte später erneut versuchen.', 503);
    }
    return reply('Erfolgreich eingetragen! Wir informieren dich zum Betatest und Produktstart.', 201);
  } catch {
    return reply('Der Wartelisten-Dienst ist momentan nicht erreichbar.', 503);
  }
}
