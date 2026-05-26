import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AppShell } from '@/components/AppShell';
import { CamerasPage } from '@/pages/CamerasPage';
import { CameraDetailPage } from '@/pages/CameraDetailPage';
import { NotFoundPage } from '@/pages/NotFoundPage';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/cameras" replace /> },
      { path: 'cameras', element: <CamerasPage /> },
      { path: 'cameras/:id', element: <CameraDetailPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
