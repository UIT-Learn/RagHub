import { useState } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { Layout, Menu, Typography } from 'antd';
import {
  DashboardOutlined, FileTextOutlined, UploadOutlined,
  MessageOutlined, ExperimentOutlined, SettingOutlined,
} from '@ant-design/icons';

const { Sider, Content, Header } = Layout;
const { Title } = Typography;

const NAV = [
  { key: '/', icon: <DashboardOutlined />, label: <Link to="/">Dashboard</Link> },
  { key: '/documents', icon: <FileTextOutlined />, label: <Link to="/documents">Documents</Link> },
  { key: '/upload', icon: <UploadOutlined />, label: <Link to="/upload">Upload</Link> },
  { key: '/chat', icon: <MessageOutlined />, label: <Link to="/chat">Chat</Link> },
  { key: '/evaluation', icon: <ExperimentOutlined />, label: <Link to="/evaluation">Evaluation</Link> },
  { key: '/settings', icon: <SettingOutlined />, label: <Link to="/settings">Settings</Link> },
];

export default function AppLayout() {
  const { pathname } = useLocation();
  const [collapsed, setCollapsed] = useState(false);

  const selected = NAV.map(n => n.key)
    .filter(k => k !== '/' ? pathname.startsWith(k) : pathname === '/')
    .at(-1) ?? '/';

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider collapsible collapsed={collapsed} onCollapse={setCollapsed}>
        <div style={{ padding: collapsed ? '16px 8px' : '16px', color: '#fff' }}>
          {!collapsed && <Title level={5} style={{ color: '#fff', margin: 0 }}>RagHub</Title>}
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[selected]} items={NAV} />
      </Sider>

      <Layout>
        <Header style={{ background: '#fff', padding: '0 24px', borderBottom: '1px solid #f0f0f0' }}>
          <Title level={4} style={{ margin: 0, lineHeight: '64px' }}>RagHub POC</Title>
        </Header>
        <Content style={{ margin: 24, padding: 24, background: '#fff', borderRadius: 8, minHeight: 360 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
