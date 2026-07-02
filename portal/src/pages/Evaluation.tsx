import { useEffect, useState } from 'react';
import {
  Alert, Button, Card, Checkbox, Empty, Input, List, Modal, Progress,
  Space, Statistic, Table, Tag, Typography, message,
} from 'antd';
import { PlayCircleOutlined, PlusOutlined, DeleteOutlined, CheckCircleOutlined } from '@ant-design/icons';
import { api, type EvaluationItem, type EvaluationSummary, type ExpectedSource, type SearchResultItem } from '../api';

const { Title, Text, Paragraph } = Typography;

function pct(v: number | null): string {
  return v === null ? '—' : `${Math.round(v * 100)}%`;
}

function toExpectedSource(r: SearchResultItem): ExpectedSource {
  return {
    documentId: r.documentId,
    documentName: r.documentName,
    headingPath: r.headingPath,
    pageNo: r.pageNo,
    snippet: r.content.slice(0, 200),
  };
}

export default function Evaluation() {
  const [items, setItems] = useState<EvaluationItem[]>([]);
  const [summary, setSummary] = useState<EvaluationSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState<Set<number>>(new Set());
  const [runningAll, setRunningAll] = useState(false);

  const [createOpen, setCreateOpen] = useState(false);
  const [question, setQuestion] = useState('');
  const [expectedAnswer, setExpectedAnswer] = useState('');
  const [searchResults, setSearchResults] = useState<SearchResultItem[]>([]);
  const [selectedChunkIds, setSelectedChunkIds] = useState<Set<number>>(new Set());
  const [searching, setSearching] = useState(false);
  const [saving, setSaving] = useState(false);

  const [verifying, setVerifying] = useState<Set<number>>(new Set());

  const [detail, setDetail] = useState<EvaluationItem | null>(null);

  function load() {
    setLoading(true);
    Promise.all([api.getEvaluations(), api.getEvaluationSummary()])
      .then(([list, sum]) => { setItems(list); setSummary(sum); })
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  function openCreate() {
    setQuestion('');
    setExpectedAnswer('');
    setSearchResults([]);
    setSelectedChunkIds(new Set());
    setCreateOpen(true);
  }

  async function handleSearch() {
    if (!question.trim()) { message.warning('Type a question first'); return; }
    setSearching(true);
    try {
      const res = await api.search(question, 10);
      setSearchResults(res.results);
    } catch {
      message.error('Search failed');
    } finally {
      setSearching(false);
    }
  }

  function toggleChunk(id: number) {
    setSelectedChunkIds(prev => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  async function handleCreate() {
    if (!question.trim()) { message.warning('Question is required'); return; }
    setSaving(true);
    try {
      const sources = searchResults
        .filter(r => selectedChunkIds.has(r.chunkId))
        .map(toExpectedSource);
      await api.createEvaluation(question, sources, expectedAnswer);
      message.success('Golden question saved');
      setCreateOpen(false);
      load();
    } catch (e: unknown) {
      message.error((e as Error).message ?? 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: number) {
    await api.deleteEvaluation(id);
    message.success('Deleted');
    load();
  }

  async function handleRun(id: number) {
    setRunning(prev => new Set(prev).add(id));
    try {
      await api.runEvaluation(id);
      load();
    } catch {
      message.error('Run failed');
    } finally {
      setRunning(prev => { const s = new Set(prev); s.delete(id); return s; });
    }
  }

  async function handleRunAll() {
    setRunningAll(true);
    try {
      await api.runAllEvaluations();
      message.success('Run-all complete');
      load();
    } catch {
      message.error('Run-all failed');
    } finally {
      setRunningAll(false);
    }
  }

  async function handleVerify(id: number) {
    setVerifying(prev => new Set(prev).add(id));
    try {
      await api.verifyEvaluation(id);
      message.success('Marked as verified');
      load();
      setDetail(prev => prev && prev.id === id ? { ...prev, verifiedAt: new Date().toISOString() } : prev);
    } catch {
      message.error('Verify failed');
    } finally {
      setVerifying(prev => { const s = new Set(prev); s.delete(id); return s; });
    }
  }

  async function handleUnverify(id: number) {
    await api.unverifyEvaluation(id);
    message.success('Verification cleared');
    load();
    setDetail(prev => prev && prev.id === id ? { ...prev, verifiedAt: null } : prev);
  }

  return (
    <div>
      <Space style={{ marginBottom: 16 }} align="center">
        <Title level={3} style={{ margin: 0 }}>Evaluation</Title>
        <Button icon={<PlusOutlined />} type="primary" onClick={openCreate}>Add Golden Question</Button>
        <Button icon={<PlayCircleOutlined />} loading={runningAll} onClick={handleRunAll}>Run All</Button>
      </Space>

      {summary && summary.runCount > 0 && (
        <Card style={{ marginBottom: 16 }}>
          <Space size={48} align="center">
            <Statistic title="Recall@k" value={pct(summary.recallAtK)} />
            <Statistic title="MRR" value={summary.mrr === null ? '—' : summary.mrr.toFixed(3)} />
            <Statistic title="Citation Accuracy" value={pct(summary.citationAccuracy)} />
            <Statistic title="Verified & Run" value={`${summary.runCount}/${summary.totalQuestions}`} />
            {summary.pendingReviewCount > 0 && (
              <Text type="warning">
                {summary.pendingReviewCount} câu đã chạy nhưng chưa verify — không tính vào số trên
              </Text>
            )}
          </Space>
        </Card>
      )}

      <Table
        loading={loading}
        dataSource={items}
        rowKey="id"
        size="small"
        columns={[
          {
            title: 'Question', dataIndex: 'question',
            render: (q: string, item: EvaluationItem) => (
              <a onClick={() => setDetail(item)}>{q}</a>
            ),
          },
          {
            title: 'Expected Sources', dataIndex: 'expectedSources',
            render: (sources: ExpectedSource[]) => sources.length > 0
              ? sources.map((s, i) => (
                  <Tag key={i}>{s.documentName}{s.pageNo != null ? ` — trang ${s.pageNo}` : ''}</Tag>
                ))
              : <Text type="secondary">(answer-only)</Text>,
          },
          {
            title: 'Retrieval', dataIndex: 'retrievalPassed',
            render: (p: boolean | null) => p === null
              ? '—'
              : <Tag color={p ? 'success' : 'error'}>{p ? 'Pass' : 'Fail'}</Tag>,
          },
          {
            title: 'Citation', dataIndex: 'citationPassed',
            render: (p: boolean | null) => p === null
              ? '—'
              : <Tag color={p ? 'success' : 'error'}>{p ? 'Pass' : 'Fail'}</Tag>,
          },
          {
            title: 'Verified', dataIndex: 'verifiedAt',
            render: (verifiedAt: string | null, item: EvaluationItem) => verifiedAt
              ? (
                <Space size={4}>
                  <Tag icon={<CheckCircleOutlined />} color="success">Verified</Tag>
                  <Button size="small" type="link" onClick={() => handleUnverify(item.id)}>Un-verify</Button>
                </Space>
              )
              : (
                <Button size="small" disabled={!item.runAt} onClick={() => setDetail(item)}>
                  {item.runAt ? 'Review & verify' : 'Run first'}
                </Button>
              ),
          },
          {
            title: 'Last Run', dataIndex: 'runAt',
            render: (t: string | null) => t ? new Date(t).toLocaleString() : 'never',
          },
          {
            title: 'Actions',
            render: (_: unknown, item: EvaluationItem) => (
              <Space>
                <Button
                  size="small" icon={<PlayCircleOutlined />}
                  loading={running.has(item.id)}
                  onClick={() => handleRun(item.id)}
                >
                  Run
                </Button>
                <Button size="small" danger icon={<DeleteOutlined />} onClick={() => handleDelete(item.id)} />
              </Space>
            ),
          },
        ]}
      />

      {/* Create golden question */}
      <Modal
        title="Add Golden Question"
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={handleCreate}
        confirmLoading={saving}
        width={700}
      >
        <Space direction="vertical" style={{ width: '100%' }}>
          <Text strong>Question</Text>
          <Space.Compact style={{ width: '100%' }}>
            <Input
              value={question}
              onChange={e => setQuestion(e.target.value)}
              placeholder="e.g. How many annual leave days do employees get?"
              onPressEnter={handleSearch}
            />
            <Button onClick={handleSearch} loading={searching}>Search chunks</Button>
          </Space.Compact>

          <Text strong style={{ marginTop: 8 }}>Expected Answer (optional)</Text>
          <Input.TextArea
            value={expectedAnswer}
            onChange={e => setExpectedAnswer(e.target.value)}
            rows={2}
            placeholder="What the correct answer should say (for reference only)"
          />

          <Text strong style={{ marginTop: 8 }}>
            Expected Sources — tick the chunk(s) that correctly answer this question
          </Text>
          <Text type="secondary" style={{ fontSize: 12 }}>
            Document, page, heading and a content snippet are snapshotted automatically — this
            stays valid even after the document is reindexed with a different chunking strategy.
          </Text>
          {searchResults.length === 0
            ? <Empty description="Search to find candidate chunks" />
            : (
              <List
                size="small"
                bordered
                style={{ maxHeight: 280, overflowY: 'auto' }}
                dataSource={searchResults}
                renderItem={r => (
                  <List.Item>
                    <Checkbox
                      checked={selectedChunkIds.has(r.chunkId)}
                      onChange={() => toggleChunk(r.chunkId)}
                    >
                      <Tag color="blue">{r.documentName}{r.pageNo != null ? ` — trang ${r.pageNo}` : ''}</Tag>
                      {r.headingPath ? ` › ${r.headingPath}` : ''}
                      <Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }} ellipsis={{ rows: 2 }}>
                        {r.content}
                      </Paragraph>
                    </Checkbox>
                  </List.Item>
                )}
              />
            )}
        </Space>
      </Modal>

      {/* Run detail + verify */}
      <Modal
        title="Run Detail"
        open={detail !== null}
        onCancel={() => setDetail(null)}
        footer={null}
        width={700}
      >
        {detail && (
          <Space direction="vertical" style={{ width: '100%' }}>
            <Text strong>Question</Text>
            <Paragraph>{detail.question}</Paragraph>

            <Text strong>Expected Sources <Text type="secondary">(ground-truth bạn đã định nghĩa)</Text></Text>
            <div>
              {detail.expectedSources.length > 0
                ? detail.expectedSources.map((s, i) => (
                    <Tag key={i}>{s.documentName}{s.pageNo != null ? ` — trang ${s.pageNo}` : ''}</Tag>
                  ))
                : <Text type="secondary">(answer-only, không gắn nguồn cụ thể)</Text>}
            </div>

            {detail.expectedAnswer && (
              <>
                <Text strong>Expected Answer</Text>
                <Paragraph type="secondary">{detail.expectedAnswer}</Paragraph>
              </>
            )}

            <Text strong>Kết quả lần chạy gần nhất</Text>
            <Space>
              {detail.retrievalPassed !== null && (
                <Tag color={detail.retrievalPassed ? 'success' : 'error'}>
                  Retrieval: {detail.retrievalPassed ? 'Pass' : 'Fail'}
                </Tag>
              )}
              {detail.citationPassed !== null && (
                <Tag color={detail.citationPassed ? 'success' : 'error'}>
                  Citation: {detail.citationPassed ? 'Pass' : 'Fail'}
                </Tag>
              )}
            </Space>

            <Text strong>Actual Retrieved Chunks (ranked)</Text>
            <div>{detail.actualChunkIds.map(id => <Tag key={id}>{id}</Tag>)}</div>

            {detail.reciprocalRank !== null && (
              <Progress percent={Math.round(detail.reciprocalRank * 100)} format={() => `RR=${detail.reciprocalRank?.toFixed(2)}`} />
            )}

            {detail.actualAnswer && (
              <>
                <Text strong>Generated Answer</Text>
                <Alert type="info" message={detail.actualAnswer} style={{ whiteSpace: 'pre-wrap' }} />
              </>
            )}

            {!detail.runAt && <Alert type="warning" message="Chưa chạy — bấm Run trong bảng trước." showIcon />}

            {detail.runAt && (
              <Alert
                type="info"
                showIcon
                message="Verify là xác nhận câu hỏi + expected source ĐÚNG (ground-truth), không phải xác nhận câu trả lời của hệ thống đúng."
                description={
                  <>
                    Nếu hệ thống trả lời sai (Pass/Fail ở trên là Fail) nhưng <b>expected source bạn chọn là đúng</b>,
                    cứ verify bình thường — đây chính là 1 lần đo "hệ thống chưa đáp ứng được", có giá trị để báo cáo.
                    Chỉ <b>không verify</b> (hoặc xoá câu hỏi và tạo lại) khi <b>chính expected source/answer bạn đã chọn bị sai</b>
                    (vd tick nhầm chunk).
                  </>
                }
              />
            )}

            <Space>
              {detail.verifiedAt
                ? <Button onClick={() => handleUnverify(detail.id)}>Un-verify</Button>
                : (
                  <Button
                    type="primary"
                    disabled={!detail.runAt}
                    loading={verifying.has(detail.id)}
                    onClick={() => handleVerify(detail.id)}
                  >
                    Verify — ground-truth này đúng
                  </Button>
                )}
            </Space>
          </Space>
        )}
      </Modal>
    </div>
  );
}
