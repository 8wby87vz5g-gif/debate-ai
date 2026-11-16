import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'DEBATE AI – Deine KI. Deine Bühne. Jede Debatte.',
  description: 'DEBATE AI: KI-Debatten für Livestream-Creator. Erlebe die interaktive Vorschau und sichere dir frühen Zugang.',
  robots: { index: true, follow: true },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="de"><body>{children}</body></html>;
}
