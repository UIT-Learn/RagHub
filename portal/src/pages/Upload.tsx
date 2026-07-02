import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Alert, Button, Card, Form, Input, Select, Typography, Upload, message,
} from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import { api, type ChunkingProfile } from '../api';

const { Title } = Typography;
const { Dragger } = Upload;

const DOC_TYPES = [
  { value: '', label: 'Auto-detect' },
  { value: 'policy', label: 'Policy (numbered headings)' },
  { value: 'technical', label: 'Technical (markdown headings)' },
  { value: 'api', label: 'API Spec (HTTP verbs)' },
];

export default function UploadPage() {
  const [form] = Form.useForm();
  const navigate = useNavigate();
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [profiles, setProfiles] = useState<ChunkingProfile[]>([]);

  useEffect(() => {
    api.getChunkingProfiles().then(list => {
      setProfiles(list);
      const def = list.find(p => p.name === 'Default');
      if (def) form.setFieldValue('chunkingProfileId', def.id);
    }).catch(() => setProfiles([]));
  }, [form]);

  async function handleSubmit(values: { category: string; docType: string; chunkingProfileId?: number }) {
    if (!file) { message.error('Please select a file.'); return; }
    setUploading(true);
    setError(null);
    try {
      const doc = await api.uploadDocument(file, values.category, values.docType, values.chunkingProfileId);
      message.success(`Uploaded "${doc.name}" — indexing in progress`);
      navigate('/documents');
    } catch (e: unknown) {
      setError((e as Error).message ?? 'Upload failed');
    } finally {
      setUploading(false);
    }
  }

  return (
    <Card style={{ maxWidth: 560 }}>
      <Title level={3}>Upload Document</Title>

      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item label="File">
          <Dragger
            beforeUpload={f => { setFile(f); return false; }}
            maxCount={1}
            accept=".pdf,.docx,.txt,.md"
            onRemove={() => setFile(null)}
          >
            <p className="ant-upload-drag-icon"><InboxOutlined /></p>
            <p className="ant-upload-text">Click or drag file here</p>
            <p className="ant-upload-hint">PDF, DOCX, TXT, MD</p>
          </Dragger>
        </Form.Item>

        <Form.Item name="category" label="Category" rules={[{ required: true, message: 'Required' }]}>
          <Input placeholder="e.g. HR, IT, Engineering" />
        </Form.Item>

        <Form.Item name="docType" label="Document Type" initialValue="">
          <Select options={DOC_TYPES} />
        </Form.Item>

        <Form.Item
          name="chunkingProfileId"
          label={<>Chunking Profile (<Link to="/settings">manage</Link>)</>}
        >
          <Select
            options={profiles.map(p => ({
              value: p.id,
              label: `${p.name} (${p.strategy}, max=${p.maxChunkSize}, overlap=${p.overlap})`,
            }))}
            placeholder="Default profile"
          />
        </Form.Item>

        {error && <Alert type="error" message={error} style={{ marginBottom: 16 }} />}

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={uploading} block>
            Upload
          </Button>
        </Form.Item>
      </Form>
    </Card>
  );
}
