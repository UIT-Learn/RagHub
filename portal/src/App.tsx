import { BrowserRouter, Route, Routes } from 'react-router-dom';
import AppLayout from './layout/AppLayout';
import Dashboard from './pages/Dashboard';
import Documents from './pages/Documents';
import UploadPage from './pages/Upload';
import DocumentDetail from './pages/DocumentDetail';
import Evaluation from './pages/Evaluation';
import Settings from './pages/Settings';
import { ChatWidget } from './components/ChatWidget';

// Standalone chat page — no admin shell, just the widget.
// Proves the component is deployment-portable (mounts with only an API base URL).
function StandaloneChat() {
  const apiBase = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api';
  return (
    <div style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      <ChatWidget apiBaseUrl={apiBase} />
    </div>
  );
}

function ChatPage() {
  return (
    <div style={{ height: 'calc(100vh - 160px)' }}>
      <ChatWidget apiBaseUrl="/api" />
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Standalone — no shell, for kiosk / edge deployment */}
        <Route path="/standalone-chat" element={<StandaloneChat />} />

        {/* Admin shell */}
        <Route element={<AppLayout />}>
          <Route path="/" element={<Dashboard />} />
          <Route path="/documents" element={<Documents />} />
          <Route path="/documents/:id" element={<DocumentDetail />} />
          <Route path="/upload" element={<UploadPage />} />
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/evaluation" element={<Evaluation />} />
          <Route path="/settings" element={<Settings />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
