import { useRef, useState } from 'react';
import {
  Alert, Button, Card, Divider, Input, List, Space, Spin, Tag, Typography,
} from 'antd';
import { LikeOutlined, DislikeOutlined, SendOutlined } from '@ant-design/icons';
import { createApiClient, type ChatHistoryTurn, type SourceChunk } from '../api';

const { Text, Paragraph } = Typography;

interface Props {
  apiBaseUrl: string;
}

interface Message {
  role: 'user' | 'assistant';
  text: string;
  sources?: SourceChunk[];
  chunkIds?: number[];
}

export function ChatWidget({ apiBaseUrl }: Props) {
  const client = useRef(createApiClient(apiBaseUrl));
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  const scrollBottom = () =>
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });

  async function send() {
    const query = input.trim();
    if (!query || loading) return;

    const history: ChatHistoryTurn[] = messages
      .filter(m => m.text.trim().length > 0)
      .map(m => ({ role: m.role, content: m.text }));

    setInput('');
    setError(null);
    setMessages(prev => [...prev, { role: 'user', text: query }]);

    const assistantIdx = messages.length + 1;
    setMessages(prev => [...prev, { role: 'assistant', text: '' }]);
    setLoading(true);

    abortRef.current = new AbortController();
    let fullText = '';
    let sources: SourceChunk[] = [];

    try {
      for await (const event of client.current.streamChat(query, history, abortRef.current.signal)) {
        if (event.type === 'token') {
          fullText += event.data.content;
          setMessages(prev => {
            const next = [...prev];
            next[assistantIdx] = { role: 'assistant', text: fullText };
            return next;
          });
          scrollBottom();
        } else if (event.type === 'sources') {
          sources = event.data.chunks;
          setMessages(prev => {
            const next = [...prev];
            next[assistantIdx] = {
              role: 'assistant',
              text: fullText,
              sources,
              chunkIds: sources.map(s => s.chunkId),
            };
            return next;
          });
        }
      }
    } catch (e: unknown) {
      if ((e as Error).name !== 'AbortError') {
        setError((e as Error).message ?? 'Request failed');
        setMessages(prev => prev.slice(0, -1)); // remove empty assistant message
      }
    } finally {
      setLoading(false);
    }
  }

  function renderText(text: string) {
    // Highlight [chunk:N] citations
    const parts = text.split(/(\[chunk:\d+\])/gi);
    return parts.map((p, i) =>
      /^\[chunk:\d+\]$/i.test(p)
        ? <Tag key={i} color="blue" style={{ fontFamily: 'monospace' }}>{p}</Tag>
        : <span key={i}>{p}</span>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 400 }}>
      {/* Message list */}
      <div style={{ flex: 1, overflowY: 'auto', padding: 16 }}>
        {messages.length === 0 && (
          <div style={{ textAlign: 'center', color: '#999', marginTop: 60 }}>
            Ask a question about your documents.
          </div>
        )}
        {messages.map((msg, i) => (
          <div key={i} style={{ marginBottom: 16, textAlign: msg.role === 'user' ? 'right' : 'left' }}>
            <Card
              size="small"
              style={{
                display: 'inline-block',
                maxWidth: '80%',
                background: msg.role === 'user' ? '#e6f4ff' : '#fafafa',
                textAlign: 'left',
              }}
            >
              {msg.role === 'assistant' && loading && i === messages.length - 1 && msg.text === '' ? (
                <Spin size="small" />
              ) : (
                <Paragraph style={{ margin: 0, whiteSpace: 'pre-wrap' }}>
                  {renderText(msg.text)}
                </Paragraph>
              )}

              {msg.sources && msg.sources.length > 0 && (
                <>
                  <Divider style={{ margin: '8px 0' }} />
                  <Text type="secondary" style={{ fontSize: 12 }}>Sources</Text>
                  <List
                    size="small"
                    dataSource={msg.sources}
                    renderItem={s => (
                      <List.Item style={{ padding: '4px 0' }}>
                        <Space>
                          <Tag color="blue">[chunk:{s.chunkId}]</Tag>
                          <Text style={{ fontSize: 12 }}>
                            {s.documentName}
                            {s.headingPath ? ` › ${s.headingPath}` : ''}
                            {s.pageNo != null ? ` (p.${s.pageNo})` : ''}
                          </Text>
                        </Space>
                      </List.Item>
                    )}
                  />
                  <Space style={{ marginTop: 8 }}>
                    <Text type="secondary" style={{ fontSize: 12 }}>Helpful?</Text>
                    <Button
                      size="small" icon={<LikeOutlined />}
                      onClick={() => client.current.submitFeedback(
                        messages[i - 1]?.text ?? '',
                        msg.text,
                        msg.chunkIds ?? [],
                        'good',
                      )}
                    />
                    <Button
                      size="small" icon={<DislikeOutlined />}
                      onClick={() => client.current.submitFeedback(
                        messages[i - 1]?.text ?? '',
                        msg.text,
                        msg.chunkIds ?? [],
                        'bad',
                      )}
                    />
                  </Space>
                </>
              )}
            </Card>
          </div>
        ))}
        {error && <Alert type="error" message={error} style={{ marginBottom: 8 }} />}
        <div ref={bottomRef} />
      </div>

      {/* Input bar */}
      <div style={{ padding: '12px 16px', borderTop: '1px solid #f0f0f0' }}>
        <Space.Compact style={{ width: '100%' }}>
          <Input
            value={input}
            onChange={e => setInput(e.target.value)}
            onPressEnter={send}
            placeholder="Ask a question…"
            disabled={loading}
          />
          <Button
            type="primary"
            icon={<SendOutlined />}
            onClick={send}
            loading={loading}
          >
            Send
          </Button>
        </Space.Compact>
      </div>
    </div>
  );
}
